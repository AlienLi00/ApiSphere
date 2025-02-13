using AS.Lib;
using AS.Models;
using System.Data;
using System.Net;
using System.Text.Json.Nodes;
using System.Text;
using Public.DB;

namespace AS.Apis
{
    public class ToDemoApi : BaseApi
    {
        #region SetData
        public override ReturnObj SetData(JsonObject billData)
        {
            //操作结果及结果描述变量
            var returnObj = new ReturnObj();
            var billType = GetBillType(billData);
            var accNo = GetAccNo(billData);
            var billDoc = Tools.GetXmlDoc($"{accNo}/{billType}");
            var xnDoc = billDoc.SelectSingleNode("/Doc");
            try
            {
                var sql = billDoc.SelectSingleNode("/Doc/Sqls/Sql[@Type='Find']").InnerText;
                var dao = Tools.GetDAO(accNo);
                var qpc = new QueryParameterCollection()
                {
                    {"iId", billData["iId"] + ""},
                    {"GUID", billData["GUID"] + ""}
                };
                var ds = dao.ExecuteDataset(sql, qpc, null);

                if (0 >= ds.Tables[0].Rows.Count)
                    throw new Exception("无数据!");

                var mainRow = ds.Tables[0].Rows[0];

                var jsonBill = new JsonObject();
                jsonBill.Add("billtype", xnDoc.Attributes["ToBillType"].Value);
                jsonBill.Add("accno", xnDoc.Attributes["ToAccNo"].Value);
                var headJo = new JsonObject();
                foreach (DataColumn dc in ds.Tables[0].Columns)
                {
                    headJo.Add(dc.ColumnName, mainRow[dc.ColumnName].ToString());
                }
                jsonBill.Add("head", headJo);
                //如果有子表
                if (1 < ds.Tables.Count)
                {
                    var jRows = new JsonArray();
                    JsonObject jRow;
                    foreach (DataRow row in ds.Tables[1].Rows)
                    {
                        jRow = new JsonObject();
                        foreach (DataColumn dc in ds.Tables[1].Columns)
                        {
                            jRow.Add(dc.ColumnName, row[dc.ColumnName].ToString());
                        }
                        jRows.Add(jRow);
                    }
                    jsonBill.Add("body", jRows);
                }
                var jsonData = jsonBill.ToJsonString();
                File.WriteAllText($"{Tools.XmlBasePath}{accNo}_{billType}_{billData["cOpTag"]}.txt", jsonData);
                String oValue = Post(xnDoc.Attributes["Url"].Value, xnDoc.Attributes["Method"].Value, jsonData);
                var jo = JsonObject.Parse(oValue).AsObject();
                if (jo["result"].ToString() == "OK")
                {
                    returnObj.Code = "0";
                    returnObj.NewBillCode = jo["newbillcode"].ToString();
                    returnObj.NewBillId = jo["newbillid"].ToString();
                    returnObj.Desc = jo["desc"].ToString();
                }
                else
                {
                    returnObj.Result = "NG";
                    returnObj.Code = "1";
                    returnObj.Desc = "返回错误:" + jo["desc"].ToString();
                }
            }
            catch (Exception e)
            {
                returnObj.Result = "NG";
                returnObj.Code = "1";
                returnObj.Desc = (e.InnerException ?? e).Message;
            }

            return returnObj;
        }
        #endregion

        #region Post
        private String Post(String url, String method, String json)
        {
            try
            {
                var req = (HttpWebRequest)WebRequest.Create(url);
                req.ContentType = "application/json;charset=UTF-8";
                req.Method = method;

                using (var sw = new StreamWriter(req.GetRequestStream()))
                {
                    sw.Write(json);
                }

                String result;
                using (var sr = new StreamReader(req.GetResponse().GetResponseStream(), Encoding.UTF8))
                {
                    result = sr.ReadToEnd();
                }

                return result;
            }
            catch (Exception ex)
            {
                throw new Exception("调用错误:" + (ex.InnerException ?? ex).Message);
            }
        }
        #endregion
    }
}
