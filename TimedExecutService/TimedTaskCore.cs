using AS.Lib;
using System.Data;
using System.Text.Json.Nodes;
using System.Xml;
using System.Threading.Tasks;
using AS.Apis;
using Public.DB;

namespace AS.TimedExecutService
{
    /// <summary>
    /// 自动执行任务的核心服务，继承自BackgroundService，用于定期执行后台任务。
    /// </summary>
    public class TimedTaskCore : BackgroundService
    {
        private bool _isExecuting;

        /// <summary>
        /// 重写 BackgroundService 的 ExecuteAsync 方法，实现每分钟检查并执行自动任务。
        /// </summary>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                
                if (!_isExecuting)
                {
                    try
                    {
                        _isExecuting = true;
                        await AutoDoAsync();
                    }
                    catch (Exception ex)
                    {
                        LogError(ex, "任务执行期间发生错误：");
                    }
                    finally
                    {
                        _isExecuting = false;
                    }
                }
            }
        }

        /// <summary>
        /// 异步执行所有自动任务。
        /// </summary>
        private async Task AutoDoAsync()
        {
            var autoTasksXml = LoadAutoTasksConfig();
            foreach (XmlNode taskXn in autoTasksXml.SelectNodes("/Config/Item[@enabled='1']"))
            {
                var defApiAttr = taskXn.Attributes["defApi"];
                if (!Equals(null, defApiAttr) && defApiAttr.Value.Equals("1"))
                {
                    await HandleCustomApi(taskXn);
                }
                else
                {
                    await HandleDefaultApi(taskXn);
                }
            }

            await ProcessPendingTasks();
        }
        
        /// <summary>
        /// 加载自动任务配置XML文档。
        /// </summary>
        /// <returns>包含自动任务配置的XML文档。</returns>
        private XmlDocument LoadAutoTasksConfig()
        {
            var apisDoc = Tools.GetXmlDoc("Apis");
            var autoTaskXmlAttr = apisDoc.SelectSingleNode("/Config/Items[@Type='Accs']")?.Attributes["AutoTaskXml"];
            if (autoTaskXmlAttr == null || string.IsNullOrEmpty(autoTaskXmlAttr.Value))
                throw new InvalidOperationException("未找到自动任务配置");

            return Tools.GetXmlDoc(autoTaskXmlAttr.Value);
        }

        /// <summary>
        /// 处理自定义API任务。
        /// </summary>
        /// <param name="task">要处理的任务节点。</param>
        private async Task HandleCustomApi(XmlNode taskXn)
        {
            var defApiClass = taskXn.InnerText;
            dynamic apiObj = Tools.GetFactory(defApiClass.Split(',')[0], defApiClass.Split(',')[1]);
            await Task.Run(() => apiObj.DoApi(taskXn));
        }

        /// <summary>
        /// 处理默认API任务。
        /// </summary>
        /// <param name="task">要处理的任务节点。</param>
        private async Task HandleDefaultApi(XmlNode task)
        {
            var maxValue = await GetMaxValue(task);
            var table = await GenerateLatestRecordTable(task, maxValue);
            await InsertIntoTaskTable(task, table);

            if (table.Rows.Count > 0)
            {
                await UpdateMaxValue(task, table);
            }
        }

        /// <summary>
        /// 获取最大值。
        /// </summary>
        /// <param name="task">任务节点。</param>
        /// <returns>最大值字符串。</returns>
        private async Task<string> GetMaxValue(XmlNode taskXn)
        {
            var sql = @"If not exists(Select 1 From T_TaskTag Where cAccId=@cAccId And cBillType=@cBillType)
                        begin
                            Insert Into T_TaskTag (cAccId,cBillType) Values(@cAccId,@cBillType)
                        end
                        Select isnull(cMaxValue,'') From T_TaskTag Where cAccId=@cAccId And cBillType=@cBillType";

            var dao = Tools.GetSysDAO();
            var qpc = new QueryParameterCollection
            {
                { "cAccId", taskXn.Attributes["accNo"].Value },
                { "cBillType", taskXn.Attributes["billType"].Value }
            };
            return (dao.ExecuteScalar(sql, qpc)?.ToString() ?? "").Trim();
        }

        /// <summary>
        /// 生成最新的记录表。
        /// </summary>
        /// <param name="task">任务节点。</param>
        /// <param name="maxValue">最大值。</param>
        /// <returns>最新记录的数据表。</returns>
        private async Task<DataTable> GenerateLatestRecordTable(XmlNode taskXn, string maxValue)
        {
            var dao = Tools.GetDAO(taskXn.Attributes["accNo"].Value);
            return dao.ExecuteDataTable(string.Format(taskXn.InnerText, maxValue));
        }

        /// <summary>
        /// 将数据插入到任务表中。
        /// </summary>
        /// <param name="task">任务节点。</param>
        /// <param name="table">数据表。</param>
        private async Task InsertIntoTaskTable(XmlNode taskXn, DataTable table)
        {
            var sql = @"if not exists(Select 1 From T_Task Where cAccId=@cAccId and iId=@iId and cBillType=@cBillType and cOpTag=@cOpTag and isnull(bOk,0)=0 And isnull(iDoCount,0)<3)
                        begin
                            Insert Into T_Task(GUID,cAccId,iId,cBillType,cOpTag,cBillTypeName,cBillCode,cDefine1,cDefine2,cDefine3,cDefine4,cDefine5) 
                            Values(NEWID(),@cAccId,@iId,@cBillType,@cOpTag,@cBillTypeName,@cBillCode,@cDefine1,@cDefine2,@cDefine3,@cDefine4,@cDefine5)
                        end";

            var dao = Tools.GetSysDAO();
            foreach (DataRow row in table.Rows)
            {
                var qpc = BuildQueryParameters(taskXn, row);
                dao.ExecuteNonQuery(sql, qpc);
            }
        }

        /// <summary>
        /// 更新最大值。
        /// </summary>
        /// <param name="task">任务节点。</param>
        /// <param name="table">数据表。</param>
        private async Task UpdateMaxValue(XmlNode task, DataTable table)
        {
            var sql = "Update T_TaskTag Set cMaxValue=@cMaxValue Where cAccId=@cAccId And cBillType=@cBillType";
            var maxValue = table.Compute($"max({task.Attributes["maxTag"].Value})", "");

            var dao = Tools.GetSysDAO();
            var qpc = new QueryParameterCollection
            {
                { "cMaxValue", maxValue },
                { "cAccId", task.Attributes["accNo"].Value },
                { "cBillType", task.Attributes["billType"].Value }
            };
            dao.ExecuteNonQuery(sql, qpc);
        }

        /// <summary>
        /// 构建查询参数集合。
        /// </summary>
        /// <param name="taskXn">任务节点。</param>
        /// <param name="row">数据行。</param>
        /// <returns>查询参数集合。</returns>
        private QueryParameterCollection BuildQueryParameters(XmlNode taskXn, DataRow row)
        {
            var qpc = new QueryParameterCollection
            {
                { "cAccId", taskXn.Attributes["accNo"].Value },
                { "cBillType", taskXn.Attributes["billType"].Value },
                { "iId", row["iId"] + "" },
                { "cOpTag", row["cOpFlag"] + "" },
                { "cBillTypeName", row.Table.Columns.Contains("cBillTypeName") ? row["cBillTypeName"] : "" },
                { "cBillCode", row.Table.Columns.Contains("cBillCode") ? row["cBillCode"] : "" },
                { "cDefine1", row.Table.Columns.Contains("cDefine1") ? row["cDefine1"] : "" },
                { "cDefine2", row.Table.Columns.Contains("cDefine2") ? row["cDefine2"] : "" },
                { "cDefine3", row.Table.Columns.Contains("cDefine3") ? row["cDefine3"] : "" },
                { "cDefine4", row.Table.Columns.Contains("cDefine4") ? row["cDefine4"] : "" },
                { "cDefine5", row.Table.Columns.Contains("cDefine5") ? row["cDefine5"] : "" }
            };
            return qpc;
        }

        /// <summary>
        /// 处理待处理的任务。
        /// </summary>
        private async Task ProcessPendingTasks()
        {
            var sql = "Select top 10000 * From T_Task Where isnull(bOk,0)=0 And isnull(iDoCount,0)<3 Order by iAutoId";
            var dao = Tools.GetSysDAO();
            var table = dao.ExecuteDataTable(sql);
            foreach (DataRow row in table.Rows)
            {
                await DoApi(row);
            }
        }

        /// <summary>
        /// 执行API调用并将结果回写到任务表。
        /// </summary>
        /// <param name="apiRow">API数据行。</param>
        private async Task DoApi(DataRow apiRow)
        {
            var joApiRow = new JsonObject();
            foreach (DataColumn dc in apiRow.Table.Columns)
            {
                joApiRow[dc.ColumnName] = apiRow[dc].ToString().Trim();
            }
            joApiRow["billtype"] = apiRow["cBillType"].ToString();
            joApiRow["accno"] = apiRow["cAccId"].ToString();

            var api = BaseApi.GetApi(joApiRow);
            var resultObj = api.SetData(joApiRow);

            // 回写调用结果
            var updLogStr = @"Update T_Task Set bOk = @bOk, cResult = @cResult, iDoCount=isnull(iDoCount,0)+1, dDoTime=getDate() Where GUID=@GUID";
            var dao = Tools.GetSysDAO();
            var qpc = new QueryParameterCollection
            {
                { "bOk", resultObj.Result.Equals("OK") ? 1 : 0 },
                { "cResult", resultObj.Desc },
                { "GUID", apiRow["GUID"].ToString().Trim() }
            };

            dao.ExecuteScalar(updLogStr, qpc);
        }

        /// <summary>
        /// 记录错误日志。
        /// </summary>
        /// <param name="ex">发生的异常。</param>
        /// <param name="messagePrefix">日志消息前缀。</param>
        private void LogError(Exception ex, string messagePrefix = "")
        {
            File.WriteAllText($"{Tools.LogFilePath}Auto_Error_{DateTime.Now:yyyyMMddHHmmss}.txt", $"{messagePrefix}{(ex.InnerException ?? ex).Message}");
        }
    }
}