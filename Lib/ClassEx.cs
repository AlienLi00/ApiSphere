using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json;

namespace AS.Lib
{
    public static class ClassEx
    {
        /// <summary>
        /// 获取指定键的 JsonNode。
        /// 如果键不存在，则抛出异常。
        /// </summary>
        public static JsonNode GetVal(this JsonNode node, string key)
        {
            var tObj = node[key];
            if (Equals(null, tObj))
                throw new Exception($"找不到键值:{key}");
            else
                return tObj;
        }

        /// <summary>
        /// 获取指定键的字符串值。
        /// 如果键不存在，可以选择返回空字符串或抛出异常。
        /// </summary>
        public static string GetStrVal(this JsonNode node, string key, bool isNoKeyReturnEmptyStr)
        {
            var jo = node as JsonObject;
            if (Equals(null, jo))
                return "";

            if (!jo.ContainsKey(key))
            {
                if (isNoKeyReturnEmptyStr)
                    return "";
                throw new Exception($"找不到键值:{key}");
            }
            else
                return jo[key].ToString();
        }
    }

    /// <summary>
    /// 用于序列化和反序列化 DateTime 类型的 JSON 转换器。
    /// </summary>
    public class DatetimeJsonConverter : JsonConverter<DateTime>
    {
        /// <summary>
        /// 从 JSON 数据读取并解析为 DateTime。
        /// </summary>
        public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String && DateTime.TryParse(reader.GetString(), out DateTime date))
                return date;
            return reader.GetDateTime();
        }

        /// <summary>
        /// 将 DateTime 写入 JSON 格式。
        /// </summary>
        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString("yyyy-MM-dd HH:mm:ss"));
        }
    }
}