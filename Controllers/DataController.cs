using Microsoft.AspNetCore.Mvc;
using System.Text.Json.Nodes;
using System.Xml;
using AS.Models;
using AS.Lib;
using AS.Apis;

namespace AS.Controllers
{
    [ApiController]
    [Route("api")]
    public class DataController : ControllerBase
    {
        #region Get
        /// <summary>
        /// 获取 JSON 格式的数据。billType\accNo
        /// </summary>
        /// <param name="billData">包含请求信息的 JSON 对象。</param>
        /// <returns>一个包含操作结果的 ReturnObj 对象。</returns>
        [HttpPost("json/get")]
        public ActionResult<ReturnObj> GetJsonData(JsonObject billData)
        {
            LogRequest(billData, "json", billData["billtype"] + "");
            return BaseApi.GetApi(billData).GetData(billData);
        }

        /// <summary>
        /// 获取 XML 格式的数据。
        /// </summary>
        /// <param name="xmlData">单据 XML 字符串对象。</param>
        /// <returns>一个包含操作结果的 ReturnObj 对象。</returns>
        [HttpPost("xml/get")]
        public ActionResult<ReturnObj> GetXmlData([FromBody] string xmlData)
        {
            var returnObj = new ReturnObj();
            try
            {
                var doc = ParseXml(xmlData, out var cBillType);
                LogRequest(doc, "xml", cBillType);

                var billData = ConvertXmlToJsonObject(doc, cBillType);
                returnObj = BaseApi.GetApi(billData).GetData(billData);
                returnObj.XmlData = GenerateResultXml(returnObj);
            }
            catch (Exception ex)
            {
                returnObj.SetError("1", ex.Message);
            }
            return returnObj;
        }
        #endregion

        #region Set
        /// <summary>
        /// 设置单据数据（JSON 格式），包括新增、修改、删除。
        /// </summary>
        /// <param name="billData">单据 JSON 对象。</param>
        /// <returns>一个包含操作结果的 ReturnObj 对象。</returns>
        [HttpPost("json/set")]
        public ActionResult<ReturnObj> SetJsonData(JsonObject billData)
        {
            LogRequest(billData, "json", billData["accno"] + "", billData["billtype"] + "");
            var resultObj = BaseApi.GetApi(billData).ExecSetData(billData);
            resultObj.CSrcSysId = GetCSrcSysId(billData);
            return resultObj;
        }

        /// <summary>
        /// 设置单据数据（XML 格式），包括新增、修改、删除。
        /// </summary>
        /// <param name="xmlData">单据 XML 字符串对象。</param>
        /// <returns>一个包含操作结果的 ReturnObj 对象。</returns>
        [HttpPost("xml/set")]
        public ActionResult<ReturnObj> SetXmlData([FromBody] string xmlData)
        {
            var returnObj = new ReturnObj();
            try
            {
                var doc = ParseXml(xmlData, out var cBillType);
                LogRequest(doc, "xml", cBillType);

                var billData = ConvertXmlToJsonObject(doc, cBillType);
                returnObj = BaseApi.GetApi(billData).ExecSetData(billData);
            }
            catch (Exception ex)
            {
                returnObj.SetError("1", ex.Message);
            }
            return returnObj;
        }
        #endregion

        #region 其他

        /// <summary>
        /// 获取外部系统的唯一标识。
        /// </summary>
        /// <param name="billData">包含请求信息的 JSON 对象。</param>
        /// <returns>外部系统的唯一标识。</returns>
        private string GetCSrcSysId(JsonObject billData)
        {
            var headJo = billData["head"]?.AsObject();
            return headJo?["csrcsysid"]?.ToString() ?? headJo?["cSrcID"]?.ToString() ?? "";
        }

        /// <summary>
        /// 记录请求日志。
        /// </summary>
        /// <param name="data">要记录的数据。</param>
        /// <param name="format">数据格式（json 或 xml）。</param>
        /// <param name="accNo">账户编号。</param>
        /// <param name="billType">账单类型。</param>
        public static void LogRequest(object data, string format, params string[] identifiers)
        {
            var filePath = $"{Tools.XmlBasePath}{string.Join("_", identifiers)}_{format.ToUpper()}_Log.txt";
            System.IO.File.WriteAllText(filePath, data.ToString());
        }

        /// <summary>
        /// 解析 XML 字符串并获取账单类型。
        /// </summary>
        /// <param name="xmlString">XML 格式的字符串。</param>
        /// <param name="cBillType">解析出的账单类型。</param>
        /// <returns>解析后的 XmlDocument。</returns>
        public static XmlDocument ParseXml(string xmlString, out string cBillType)
        {
            var doc = new XmlDocument();
            doc.LoadXml(xmlString);
            cBillType = doc.SelectSingleNode("/Doc").Attributes["BillType"].Value;
            return doc;
        }

        /// <summary>
        /// 将 XML 转换为 JSON 对象。
        /// </summary>
        /// <param name="doc">XmlDocument 实例。</param>
        /// <param name="cBillType">账单类型。</param>
        /// <returns>转换后的 JsonObject。</returns>
        public static JsonObject ConvertXmlToJsonObject(XmlDocument doc, string cBillType)
        {
            var billData = new JsonObject
            {
                ["billtype"] = cBillType,
                ["accno"] = doc.SelectSingleNode("/Doc").Attributes["AccNo"]?.Value ?? "",
                ["where"] = doc.SelectSingleNode("/Doc/Where")?.InnerText.Trim() ?? ""
            };

            // Add head section if it exists
            var headJo = new JsonObject();
            var headItems = doc.SelectSingleNode("/Doc/Head")?.ChildNodes;
            foreach (XmlNode xn in headItems?.Cast<XmlNode>() ?? Enumerable.Empty<XmlNode>()) // 使用 LINQ 方法
            {
                headJo[xn.LocalName] = xn.InnerText.Trim();
            }
            if (headJo.Count > 0) billData["head"] = headJo;

            // Add body section if it exists
            var bodyJos = new JsonArray();
            foreach (XmlNode xn in doc.SelectNodes("/Doc/Body/Row"))
            {
                var bodyJo = new JsonObject();
                foreach (XmlNode xnField in xn.ChildNodes)
                {
                    bodyJo[xnField.LocalName] = xnField.InnerText.Trim();
                }
                if (bodyJo.Count > 0) bodyJos.Add(bodyJo);
            }
            if (bodyJos.Count > 0) billData["body"] = bodyJos;

            return billData;
        }

        /// <summary>
        /// 生成结果 XML。
        /// </summary>
        /// <param name="returnObj">包含操作结果的 ReturnObj 对象。</param>
        /// <returns>结果 XML 字符串。</returns>
        public static string GenerateResultXml(ReturnObj returnObj)
        {
            var dataDoc = new XmlDocument();
            var declaration = dataDoc.CreateXmlDeclaration("1.0", "UTF-8", null);
            var rootNode = dataDoc.CreateNode(XmlNodeType.Element, "Doc", "");
            dataDoc.AppendChild(rootNode);
            dataDoc.InsertBefore(declaration, dataDoc.DocumentElement);

            if (returnObj.Data != null && returnObj.Data.Count > 0)
            {
                foreach (var jRow in returnObj.Data)
                {
                    var rowXn = dataDoc.CreateNode(XmlNodeType.Element, "Row", "");
                    foreach (var item in jRow.AsObject())
                    {
                        var itemXn = dataDoc.CreateNode(XmlNodeType.Element, item.Key, "");
                        itemXn.InnerText = item.Value.ToString().Trim();
                        rowXn.AppendChild(itemXn);
                    }
                    dataDoc.SelectSingleNode("/Doc").AppendChild(rowXn);
                }
            }

            return dataDoc.OuterXml;
        }
        #endregion
    }
}