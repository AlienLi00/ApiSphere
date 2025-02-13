using AS.Lib;
using AS.Models;
using Public.DB;
using System.Data;
using System.Text.Json.Nodes;
using System.Xml;

namespace AS.Apis
{
    /// <summary>
    /// DemoApi 类提供了与账单数据交互的 API 方法。
    /// </summary>
    public class DemoApi : BaseApi
    {
        /// <summary>
        /// 设置账单数据，并将其保存到数据库中。
        /// </summary>
        /// <param name="billData">包含账单信息的 JSON 对象。</param>
        /// <returns>返回一个包含操作结果的对象。</returns>
        public override ReturnObj SetData(JsonObject billData)
        {
            var returnObj = new ReturnObj();
            var apisDoc = Tools.GetXmlDoc("Apis");
            var accNo = GetAccNo(billData);
            var billType = GetBillType(billData);

            // Validate account and bill type
            if (apisDoc.SelectSingleNode($"/Config/Items[@Type='Accs']/Item[@AccNo='{accNo}']") == null ||
                apisDoc.SelectSingleNode($"/Config/Items[@Type='Doc']/Item[@key='{billType}']") == null)
            {
                throw new Exception($"Invalid account number or bill type: {accNo}, {billType}");
            }

            var billXn = apisDoc.SelectSingleNode($"/Config/Items[@Type='Doc']/Item[@key='{billType}']");
            var billDoc = Tools.GetXmlDoc($"{accNo}/{billType}");
            var headJo = billData["head"].AsObject();

            // Set default maker if not provided
            headJo["cMaker"] = headJo.ContainsKey("cMaker") && !string.IsNullOrEmpty(headJo["cMaker"] + "")
                ? headJo["cMaker"]
                : billXn.Attributes["cMaker"].Value;

            var dao = Tools.GetDAO(accNo);
            try
            {
                dao.BeginTransaction(accNo);
                var idTable = ToMainTable(billData, billDoc, dao, accNo);
                ToSubTable(billData, billDoc, idTable, dao, accNo);
                AfterDo(billDoc, idTable, dao, accNo);

                returnObj.Desc = returnObj.NewBillCode = idTable.Rows[0]["cCode"].ToString().Trim();
                returnObj.NewBillId = idTable.Rows[0]["iId"].ToString().Trim();
                dao.TransCommit(accNo);
            }
            catch (Exception ex)
            {
                returnObj.Result = "NG";
                returnObj.Code = "1";
                returnObj.Desc = ex.Message;
                dao.TransRollback(accNo);
            }

            return returnObj;
        }

        /// <summary>
        /// 将账单数据写入主表。
        /// </summary>
        /// <param name="billData">包含账单信息的 JSON 对象。</param>
        /// <param name="billDoc">描述账单结构的 XML 文档。</param>
        /// <param name="dao">用于执行数据库操作的数据访问对象。</param>
        /// <param name="accNo">账户编号。</param>
        /// <returns>包含新插入记录 ID 的 DataTable。</returns>
        private DataTable ToMainTable(JsonObject billData, XmlDocument billDoc, DAO dao, string accNo)
        {
            var headJo = billData["head"].AsObject();
            var bodyJos = billData.ContainsKey("body") ? billData["body"].AsArray() : new JsonArray();

            var paras = new QueryParameterCollection();
            foreach (var item in headJo)
            {
                paras.Add(item.Key, item.Value);
            }

            paras.Add("cMaker", headJo["cMaker"]);
            paras.Add("iRows", bodyJos.Count, DbType.Int32);

            var headSql = billDoc.SelectSingleNode("/Doc/Sqls/Sql[@Type='SaveHead']").InnerText;
            return dao.ExecuteDataTable(headSql, paras, accNo);
        }

        /// <summary>
        /// 将账单数据写入子表。
        /// </summary>
        /// <param name="billData">包含账单信息的 JSON 对象。</param>
        /// <param name="billDoc">描述账单结构的 XML 文档。</param>
        /// <param name="idTable">包含主表记录 ID 的 DataTable。</param>
        /// <param name="dao">用于执行数据库操作的数据访问对象。</param>
        /// <param name="accNo">账户编号。</param>
        private static void ToSubTable(JsonObject billData, XmlDocument billDoc, DataTable idTable, DAO dao, string accNo)
        {
            if (!billData.ContainsKey("body")) return;

            if (idTable == null) return;

            var bodyJos = billData["body"].AsArray();
            var id = idTable.Rows[0]["iId"].ToString().Trim();
            int ids = Convert.ToInt32(idTable.Rows[0]["iIds"]);

            var bodySql = billDoc.SelectSingleNode("/Doc/Sqls/Sql[@Type='SaveBody']")?.InnerText;
            if (string.IsNullOrEmpty(bodySql)) return;

            foreach (var row in bodyJos)
            {
                var paras = new QueryParameterCollection();
                paras.Add("iId", id);
                paras.Add("iIds", ++ids, DbType.Int32);
                paras.Add("iRowNo", row["iRowNo"], DbType.Int32);

                foreach (var item in row.AsObject())
                {
                    paras.Add(item.Key, item.Value == null || item.Value.ToString().Trim() == "-"
                        ? DBNull.Value
                        : item.Value);
                }

                foreach (DataColumn dc in idTable.Columns)
                {
                    paras.Add("m_" + dc.ColumnName, idTable.Rows[0][dc] ?? DBNull.Value);
                }

                dao.ExecuteNonQuery(bodySql, paras, accNo);
            }
        }

        /// <summary>
        /// 执行账单保存后的后续处理逻辑。
        /// </summary>
        /// <param name="billDoc">描述账单结构的 XML 文档。</param>
        /// <param name="idTable">包含主表记录 ID 的 DataTable。</param>
        /// <param name="dao">用于执行数据库操作的数据访问对象。</param>
        /// <param name="accNo">账户编号。</param>
        private static void AfterDo(XmlDocument billDoc, DataTable idTable, DAO dao, string accNo)
        {
            var afterNode = billDoc.SelectSingleNode("/Doc/Sqls/Sql[@Type='AfterSave']");
            var afterSql = afterNode?.InnerText;
            if (!string.IsNullOrEmpty(afterSql))
            {
                dao.ExecuteNonQuery(afterSql, new QueryParameterCollection { { "iId", idTable.Rows[0]["iId"] } }, accNo);
            }
        }
    }
}