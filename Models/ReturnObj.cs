using System.Text.Json.Nodes;
using System.Xml.Linq;

namespace AS.Models
{
    /// <summary>
    /// API 接口返回对象类，用于封装 API 操作的结果信息。
    /// </summary>
    public class ReturnObj
    {
        /// <summary>
        /// 初始化 ReturnObj 类的新实例。
        /// </summary>
        public ReturnObj()
        {
            Result = "OK";
            Code = "0";
            Desc = "";
            Data = new JsonArray();
            XmlData = "";
            NewBillId = "";
            NewBillCode = "";
            CSrcSysId = "";
            Time = DateTime.Now;
            Token = "";
        }

        /// <summary>
        /// 获取或设置操作结果是否成功，默认为 "OK" 表示成功。
        /// </summary>
        public string Result { get; set; }

        /// <summary>
        /// 获取或设置操作结果标识，默认为 "0" 表示成功，非零表示失败。
        /// </summary>
        public string Code { get; set; }

        /// <summary>
        /// 获取或设置结果描述，默认为空字符串。
        /// </summary>
        public string Desc { get; set; }

        /// <summary>
        /// 获取或设置查询后 JSON 数组形式的结果数据，默认为空数组。
        /// </summary>
        public JsonArray Data { get; set; }

        /// <summary>
        /// 获取或设置查询后 XML 形式的结果数据，默认为空字符串。
        /// </summary>
        public string XmlData { get; set; }

        /// <summary>
        /// 获取或设置生成的单据 ID，默认为空字符串。
        /// </summary>
        public string NewBillId { get; set; }

        /// <summary>
        /// 获取或设置生成的单据编号，默认为空字符串。
        /// </summary>
        public string NewBillCode { get; set; }

        /// <summary>
        /// 获取或设置外部系统传入的唯一标识，默认为空字符串。
        /// </summary>
        public string CSrcSysId { get; set; }

        /// <summary>
        /// 获取或设置当前时间，默认为对象创建时的时间。
        /// </summary>
        public DateTime Time { get; set; }

        /// <summary>
        /// 获取或设置令牌，默认为空字符串。
        /// </summary>
        public string Token { get; set; }

        /// <summary>
        /// 设置错误信息。
        /// </summary>
        /// <param name="code">错误码。</param>
        /// <param name="desc">错误描述。</param>
        public void SetError(string code, string desc)
        {
            Result = "NG";
            Code = code;
            Desc = desc;
        }

        /// <summary>
        /// 设置成功信息。
        /// </summary>
        /// <param name="data">成功的数据。</param>
        /// <param name="newBillId">新生成的单据 ID。</param>
        /// <param name="newBillCode">新生成的单据编号。</param>
        /// <param name="cSrcSysId">外部系统传入的唯一标识。</param>
        public void SetSuccess(JsonArray data, string newBillId = "", string newBillCode = "", string cSrcSysId = "")
        {
            Result = "OK";
            Code = "0";
            Data = data;
            NewBillId = newBillId;
            NewBillCode = newBillCode;
            CSrcSysId = cSrcSysId;
        }
    }
}