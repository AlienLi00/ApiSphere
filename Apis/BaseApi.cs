using AS.Lib;
using AS.Models;
using Public.DB;
using System.Data;
using System.Text.Json.Nodes;
using System.Xml;

namespace AS.Apis
{
    /// <summary>
    /// 基础 API 抽象类，提供数据获取、设置及日志记录的基本功能。
    /// </summary>
    public abstract class BaseApi
    {
        #region Public Methods

        /// <summary>
        /// 根据传入的 JSON 数据获取相应的数据。
        /// </summary>
        /// <param name="billData">包含请求信息的 JSON 对象。</param>
        /// <returns>一个包含操作结果的 ReturnObj 对象。</returns>
        public virtual ReturnObj GetData(JsonObject billData)
        {
            var returnObj = new ReturnObj();
            try
            {
                InitializeContext(billData);

                // Token 检查
                if (IsTokenCheckRequired())
                {
                    var token = GetBillToken(billData);
                    if (!ApiToken.CheckToken(token))
                        return new ReturnObj { Result = "NG", Code = "1", Desc = "无效token" };
                    else
                        ApiToken.UpdateToken(token);
                }

                ValidateAccount();

                GenerateData(billData, out var data);
                returnObj.Data = data;
            }
            catch (Exception e)
            {
                returnObj.Result = "NG";
                returnObj.Code = "1";
                returnObj.Desc = (e.InnerException ?? e).Message;
            }

            return returnObj;
        }

        /// <summary>
        /// 执行设置数据操作，并记录日志。
        /// </summary>
        /// <param name="billData">包含请求信息的 JSON 对象。</param>
        /// <returns>一个包含操作结果的 ReturnObj 对象。</returns>
        public virtual ReturnObj ExecSetData(JsonObject billData)
        {
            InitializeContext(billData);

            // Token 检查
            if (IsTokenCheckRequired())
            {
                var token = GetBillToken(billData);
                if (!ApiToken.CheckToken(token))
                    return new ReturnObj { Result = "NG", Code = "1", Desc = "无效token", CSrcSysId = GetSrcSysId(billData) };
                else
                    ApiToken.UpdateToken(token);
            }

            CheckDuplicateEntry();

            var returnObj = SetData(billData);
            WriteOperationLog(returnObj, billData);

            return returnObj;
        }

        /// <summary>
        /// 传入数据生成单据，具体实现由子类提供。
        /// </summary>
        /// <param name="billData">包含请求信息的 JSON 对象。</param>
        /// <returns>一个包含操作结果的 ReturnObj 对象。</returns>
        public abstract ReturnObj SetData(JsonObject billData);

        #endregion

        #region Private Methods

        /// <summary>
        /// 初始化上下文，包括账户编号、账单类型和账单文档。
        /// </summary>
        /// <param name="billData">包含请求信息的 JSON 对象。</param>
        private void InitializeContext(JsonObject billData)
        {
            _accNo = GetAccNo(billData);
            _billType = GetBillType(billData);
            _billDoc = Tools.GetXmlDoc($"{_accNo}/{_billType}");
        }

        /// <summary>
        /// 检查是否需要进行 Token 验证。
        /// </summary>
        /// <returns>如果需要验证，则返回 true；否则返回 false。</returns>
        private bool IsTokenCheckRequired()
        {
            var isCheckTokenAttr = _billDoc.SelectSingleNode("/Doc").Attributes["IsCheckToken"];
            return !Equals(null, isCheckTokenAttr) && isCheckTokenAttr.Value.Equals("1");
        }

        /// <summary>
        /// 验证账户是否存在。
        /// </summary>
        /// <exception cref="Exception">当账户不存在时抛出异常。</exception>
        private void ValidateAccount()
        {
            var apisDoc = Tools.GetXmlDoc("Apis");
            var accXn = apisDoc.SelectSingleNode($"/Config/Items[@Type='Accs']/Item[@AccNo='{_accNo}']");
            if (Object.Equals(null, accXn))
            {
                throw new Exception($"账套号不存在!{_accNo}");
            }
        }

        /// <summary>
        /// 从数据库中生成并返回数据。
        /// </summary>
        /// <param name="billData">包含请求信息的 JSON 对象。</param>
        /// <param name="data">输出参数，包含查询到的数据。</param>
        private void GenerateData(JsonObject billData, out JsonArray data)
        {
            var findSql = _billDoc.SelectSingleNode("/Doc/Sqls/Sql[@Type='Find']").InnerText;
            var whereSql = billData["where"] + "";
            if (!String.IsNullOrEmpty(whereSql))
                whereSql = "And " + whereSql;

            findSql = String.Format(findSql, whereSql);

            var table = new DataTable();
            var dao = Tools.GetDAO(_accNo);
            table = dao.ExecuteDataset(findSql).Tables[0];

            data = ConvertTableToJsonArray(table);
        }

        /// <summary>
        /// 将 DataTable 转换为 JsonArray。
        /// </summary>
        /// <param name="table">要转换的 DataTable。</param>
        /// <returns>转换后的 JsonArray。</returns>
        private JsonArray ConvertTableToJsonArray(DataTable table)
        {
            var jsonArray = new JsonArray();
            foreach (DataRow row in table.Rows)
            {
                var rowJo = new JsonObject();
                foreach (DataColumn dc in table.Columns)
                {
                    rowJo[dc.ColumnName] = row[dc] + "";
                }
                jsonArray.Add(rowJo);
            }
            return jsonArray;
        }

        /// <summary>
        /// 检查是否有重复条目存在。
        /// </summary>
        /// <exception cref="DuplicateEntryException">当检测到重复条目时抛出异常。</exception>
        private void CheckDuplicateEntry()
        {
            var cSrcSysId = GetSrcSysId();
            if (!String.IsNullOrEmpty(cSrcSysId))
            {
                var dao = Tools.GetSysDAO();
                var sql = "Select cResult,cNewCode,cNewId From T_Logs Where cSrcId=@cSrcId And isnull(bOk,0)=1 And cAccId=@cAccId And cBusId=@cBusId";
                var parameters = new QueryParameterCollection
                {
                    { "cSrcId", cSrcSysId },
                    { "cAccId", _accNo },
                    { "cBusId", _billType }
                };

                var table = dao.ExecuteDataset(sql, parameters).Tables[0];
                if (table.Rows.Count > 0)
                {
                    throw new DuplicateEntryException($"已有相同外部系统ID的数据存在: {table.Rows[0]["cResult"]}");
                }
            }
        }

        /// <summary>
        /// 获取外部系统的 ID。
        /// </summary>
        /// <param name="billData">包含请求信息的 JSON 对象。</param>
        /// <returns>外部系统的 ID。</returns>
        private string GetSrcSysId(JsonObject billData = null)
        {
            var headJo = billData?["head"].AsObject();
            return headJo?["cSrcID"] + "" ?? GetSrcSysId();
        }

        /// <summary>
        /// 写入操作日志。
        /// </summary>
        /// <param name="returnObj">包含操作结果的 ReturnObj 对象。</param>
        private void WriteOperationLog(ReturnObj returnObj, JsonObject billData)
        {
            var writeDataLog = _billDoc.SelectSingleNode("/Doc").Attributes["WriteDataLog"]?.Value == "1";
            ToLog(
                result: returnObj.Result,
                desc: returnObj.Desc,
                accNo: _accNo,
                billType: _billType,
                opType: "SetData",
                maker: "",
                newId: returnObj.NewBillId,
                newCode: returnObj.NewBillCode,
                writeDataLog: writeDataLog,
                srcSysId: returnObj.CSrcSysId,
                billData: billData.ToJsonString()
            );
        }

        /// <summary>
        /// 写入日志到数据库或文件。
        /// </summary>
        /// <param name="result">操作结果。</param>
        /// <param name="desc">描述信息。</param>
        /// <param name="accNo">账户编号。</param>
        /// <param name="billType">账单类型。</param>
        /// <param name="opType">操作类型。</param>
        /// <param name="maker">操作人。</param>
        /// <param name="newId">新生成的 ID。</param>
        /// <param name="newCode">新生成的编码。</param>
        /// <param name="writeDataLog">是否写入数据日志。</param>
        /// <param name="srcSysId">来源系统的 ID。</param>
        /// <param name="billData">原始 JSON 数据。</param>
        private void ToLog(string result, string desc, string accNo, string billType, string opType, string maker, string newId, string newCode, bool writeDataLog, string srcSysId, string billData)
        {
            var sql = @"
                INSERT INTO T_Logs (
                    GUID, dTime, cAccId, cUserId, cBusId, cBusName, cOpType, cNewId, cNewCode, cSrcId, cSrcCode, bOk, cResult, cXml
                ) VALUES (
                    NEWID(), GETDATE(), @cAccId, @cUserId, @cBusId, '', @cOpType, @cNewId, @cNewCode, @cSrcId, '', @bOk, @cResult, @cXml
                )";

            var qpc = new QueryParameterCollection
            {
                { "cAccId", accNo },
                { "cUserId", maker },
                { "cBusId", billType },
                { "cOpType", opType },
                { "cNewId", newId },
                { "cNewCode", newCode },
                { "cSrcId", srcSysId },
                { "bOk", result.Equals("OK") ? 1 : 0 },
                { "cResult", desc },
                { "cXml", writeDataLog ? billData : "" }
            };

            try
            {
                var dao = Tools.GetSysDAO();
                dao.ExecuteNonQuery(sql, qpc, "");
            }
            catch (Exception ex)
            {
                File.WriteAllText($"{Tools.LogFilePath}{accNo}_{billType}_Error_{DateTime.Now:yyyyMMddHHmmss}.txt", $"PonderWS: {ex.Message}");
            }
        }

        /// <summary>
        /// 根据规则获取值，支持从 JSON 元素、静态值或 SQL 查询中取值。
        /// </summary>
        /// <param name="billDocXn">取值规则行。</param>
        /// <param name="billData">包含请求信息的 JSON 对象。</param>
        /// <param name="curJo">当前处理的 JSON 对象。</param>
        /// <returns>根据规则获取到的值。</returns>
        private string GetJsonValue(XmlNode billDocXn, JsonObject billData, JsonObject curJo)
        {
            var cValueType = billDocXn.Attributes["ValueType"]?.Value ?? "0";
            var cValueExp = billDocXn.Attributes["Value"]?.Value ?? "";

            switch (cValueType)
            {
                case "0": // JSON元素值
                    return curJo.GetStrVal(cValueExp, false);
                case "1": // 静态值
                    return cValueExp;
                case "2": // SQL取值
                    var sql = ReplaceVariablesInSql(cValueExp, curJo, billData["head"]?.AsObject());
                    var dao = Tools.GetDAO(GetAccNo(billData));
                    var obj = dao.ExecuteScalar(sql);
                    return obj != null ? obj.ToString() : "";
                default:
                    throw new ArgumentOutOfRangeException(nameof(cValueType), cValueType, "未知的取值类型");
            }
        }

        /// <summary>
        /// 替换 SQL 查询中的变量为实际值。
        /// </summary>
        /// <param name="sql">SQL 查询语句。</param>
        /// <param name="curJo">当前处理的 JSON 对象。</param>
        /// <param name="headJo">表头的 JSON 对象。</param>
        /// <returns>替换变量后的 SQL 查询语句。</returns>
        private string ReplaceVariablesInSql(string sql, JsonObject curJo, JsonObject headJo = null)
        {
            foreach (var jo in curJo.Concat(headJo ?? Enumerable.Empty<KeyValuePair<string, JsonNode>>()))
            {
                sql = sql.Replace($"[{jo.Key}]", jo.Value + "", StringComparison.OrdinalIgnoreCase);
                if (headJo != null)
                {
                    sql = sql.Replace($"[head.{jo.Key}]", jo.Value + "", StringComparison.OrdinalIgnoreCase);
                }
            }
            return sql;
        }

        #endregion

        #region Static Methods

        /// <summary>
        /// 根据账单类型和账户编号获取对应的 API 实例。
        /// </summary>
        /// <param name="billData">包含请求信息的 JSON 对象。</param>
        /// <returns>BaseApi 的实例。</returns>
        public static BaseApi GetApi(JsonObject billData)
        {
            var billType = GetBillType(billData);
            return GetApi(billType, GetAccNo(billData));
        }

        /// <summary>
        /// 根据账单类型和账户编号获取对应的 API 实例。
        /// </summary>
        /// <param name="billType">账单类型。</param>
        /// <param name="accNo">账户编号。</param>
        /// <returns>BaseApi 的实例。</returns>
        public static BaseApi GetApi(string billType, string accNo)
        {
            var billDoc = Tools.GetXmlDoc($"{accNo}/{billType}");
            var apiName = billDoc.SelectSingleNode("/Doc").Attributes["ApiName"].Value;
            var type = Type.GetType("AS.Apis." + apiName);
            return Activator.CreateInstance(type) as BaseApi;
        }

        /// <summary>
        /// 获取账户编号。
        /// </summary>
        /// <param name="billData">包含请求信息的 JSON 对象。</param>
        /// <returns>账户编号。</returns>
        public static string GetAccNo(JsonObject billData)
        {
            var accNo = billData["accno"] + "";
            if (string.IsNullOrEmpty(accNo))
            {
                var apisDoc = Tools.GetXmlDoc("Apis");
                accNo = apisDoc.SelectSingleNode("/Config/Items[@Type='Accs']")?.Attributes["DefaultAccNo"]?.Value;
            }
            return accNo;
        }

        /// <summary>
        /// 获取账单类型。
        /// </summary>
        /// <param name="billData">包含请求信息的 JSON 对象。</param>
        /// <returns>账单类型。</returns>
        public static string GetBillType(JsonObject billData)
        {
            return billData["billtype"] + "";
        }

        /// <summary>
        /// 获取账单 Token。
        /// </summary>
        /// <param name="billData">包含请求信息的 JSON 对象。</param>
        /// <returns>账单 Token。</returns>
        public static string GetBillToken(JsonObject billData)
        {
            return billData["token"] + "";
        }

        #endregion

        #region Fields

        /// <summary>
        /// 当前账户编号。
        /// </summary>
        private string _accNo;

        /// <summary>
        /// 当前账单类型。
        /// </summary>
        private string _billType;

        /// <summary>
        /// 当前账单文档。
        /// </summary>
        private XmlDocument _billDoc;

        #endregion

        #region Custom Exceptions

        /// <summary>
        /// 表示检测到重复条目的异常。
        /// </summary>
        private class DuplicateEntryException : Exception
        {
            /// <summary>
            /// 初始化 DuplicateEntryException 类的新实例。
            /// </summary>
            /// <param name="message">说明异常原因的消息。</param>
            public DuplicateEntryException(string message) : base(message) { }
        }

        #endregion
    }
}