using Public.DB;
using Public.Tools;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Xml;

namespace AS.Lib
{
    public class Tools
    {
        #region 获取Xml路径
        /// <summary>
        /// 获取Xml路径
        /// </summary>
        public static String XmlBasePath
        {
            get
            {
                return AppDomain.CurrentDomain.BaseDirectory + @"Xmls\";
            }
        }
        #endregion

        #region 获取Xml路径
        /// <summary>
        /// 获取Xml路径
        /// </summary>
        public static String LogFilePath
        {
            get
            {
                return XmlBasePath + @"Logs\";
            }
        }
        #endregion

        #region 获取xmlDoc
        public static XmlDocument GetXmlDoc(String fileName)
        {
            var doc = new XmlDocument();
            var fullPath = XmlBasePath + fileName;
            if (!fullPath.Substring(fullPath.Length - 4).ToUpper().Equals(".XML"))
                fullPath += ".xml";
            doc.Load(fullPath);
            return doc;
        }
        #endregion

        #region 保存xmlDoc
        public static void SaveXmlDoc(XmlDocument doc, String fileName)
        {
            var fullPath = XmlBasePath + fileName;
            if (!fullPath.Substring(fullPath.Length - 4).ToUpper().Equals(".XML"))
                fullPath += ".xml";
            doc.Save(fullPath);
        }
        #endregion

        #region 安全

        #region MD5加密
        /// <summary>
        /// MD5加密
        /// </summary>
        /// <param name="res"></param>
        /// <returns></returns>
        public static string Encrypt(string res)
        {
            var result = "";
            using (var md5 = MD5.Create())
            {
                result = BitConverter.ToString(md5.ComputeHash(Encoding.UTF8.GetBytes(res))).Replace("-", "");
            }
            return result;
        }
        #endregion

        #region 64位编码
        public static string Base64Encode(string str)
        {
            byte[] barray;
            barray = Encoding.Default.GetBytes(str);
            return Convert.ToBase64String(barray);
        }
        #endregion

        #region 64位解码
        public static string Base64Decode(string str)
        {
            byte[] barray;
            barray = Convert.FromBase64String(str);
            return Encoding.Default.GetString(barray);
        }
        #endregion

        #region 加密解密
        public static string EPwd(string res)
        {
            //如果是空密码则不进行加密
            if (String.IsNullOrEmpty(res))
                return res;
            res += "ponder";
            string pwd = Base64Encode(res);
            //功能举例说明:字符串"abcdefd"中，"fd"与"bc"调换位置
            pwd = ChangeStringPosi(pwd, 6, 2, 2);
            //进一步编码
            pwd = Base64Encode(pwd);
            return pwd;
        }

        public static string DPwd(string pwd)
        {
            //如果是空密码则不进行加密
            if (String.IsNullOrEmpty(pwd))
                return pwd;

            string res = "";
            try
            {
                res = Base64Decode(pwd);
                //功能举例说明:字符串"abcdefd"中，"fd"与"bc"调换位置
                res = ChangeStringPosi(res, 6, 2, 2);
                //进一步编码
                res = Base64Decode(res);
                res = res.Substring(0, res.Length - "ponder".Length);
            }
            catch (Exception ex)
            {
                throw ex;
            }
            return res;
        }

        #endregion

        #region 字符串中的字符位置调换
        //功能举例说明:字符串"abcdefd"中，"fd"与"bc"调换位置
        public static string ChangeStringPosi(string resStr, int bakPos1, int bakPos2, int length)
        {
            string a01 = resStr.Substring(0, resStr.Length - bakPos1);
            string a02 = resStr.Substring(resStr.Length - bakPos1, length);
            string a03 = resStr.Substring(resStr.Length - bakPos1 + length, bakPos1 - bakPos2 - length);
            string a04 = resStr.Substring(resStr.Length - length);
            return a01 + a04 + a03 + a02;
        }
        #endregion

        #endregion

        #region 通过反射创建对象
        /// <summary>
        /// 通过反射创建对象
        /// </summary>
        /// <param name="classInfo">类型信息</param>
        /// <returns></returns>
        public static Object GetFactory(String assemblyName, String fullClassName, Object[] args)
        {
            var assembly = Assembly.Load(assemblyName);
            var type = assembly.GetType(fullClassName);
            Object obj = Activator.CreateInstance(type, args);
            return obj;
        }
        public static Object GetFactory(String assemblyName, String fullClassName)
        {
            return GetFactory(assemblyName, fullClassName, null);
        }
        #endregion

        #region 获取账套库DAO
        public static DAO GetDAO(String accNo)
        {
            var dp = new DatabaseProperty();
            try
            {
                var apisDoc = Tools.GetXmlDoc("Apis");
                var accXn = apisDoc.SelectSingleNode($"/Config/Items[@Type='Accs']/Item[@AccNo='{accNo}']");
                var dbTypeAttr = accXn.Attributes["DbType"];
                var connString = "";
                if (Equals(null, dbTypeAttr) || dbTypeAttr.Value.Equals("0"))//sqlserver
                {
                    connString = $"server={accXn.Attributes["Server"].Value};database={accXn.Attributes["DBName"].Value};uid={accXn.Attributes["UserID"].Value};pwd={SimpleAesEncryption.Decrypt(accXn.Attributes["Pwd"].Value)};MultipleActiveResultSets=True";
                    dp.DatabaseType = DatabaseType.MSSQLServer;
                }
                else if (dbTypeAttr.Value.Equals("1")) //oracle
                {
                    //Oracle连接字符串
                    connString = $"Data Source=(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST={accXn.Attributes["Server"].Value})(PORT={accXn.Attributes["Port"].Value}))(CONNECT_DATA=(SERVICE_NAME={accXn.Attributes["ServiceName"].Value})));Persist Security Info=True;User ID={accXn.Attributes["UserID"].Value};Password={SimpleAesEncryption.Decrypt(accXn.Attributes["Pwd"].Value)};";
                    dp.DatabaseType = DatabaseType.Oracle;
                }
                dp.ConnectionString = connString;
                dp.Token = accNo;
            }
            catch (Exception ex)
            {
                File.WriteAllText(String.Format("{0}_Err_GetConn.txt", Tools.LogFilePath), ex.Message);
            }
            return DAO.GetDAO(dp);
        }

        #region 获取系统库DAO
        public static DAO GetSysDAO()
        {
            var dp = new DatabaseProperty();
            try
            {
                var apisDoc = Tools.GetXmlDoc("Apis");
                var dbXn = apisDoc.SelectSingleNode($"/Config/Item[@Type='DB']");
                var connString = $"server={dbXn.Attributes["Server"].Value};database={dbXn.Attributes["DBName"].Value};uid={dbXn.Attributes["UserID"].Value};pwd={SimpleAesEncryption.Decrypt(dbXn.Attributes["Pwd"].Value)};MultipleActiveResultSets=True";
                dp.ConnectionString = connString;
                dp.Token = "sys";
                dp.DatabaseType = DatabaseType.MSSQLServer;
            }
            catch (Exception ex)
            {
                File.WriteAllText(String.Format("{0}_Err_GetConn.txt", Tools.LogFilePath), ex.Message);
            }
            return DAO.GetDAO(dp);
        }
        #endregion
        #endregion

        #region Post
        /// <summary>
        /// 异步调用Post方法
        /// </summary>
        /// <param name="url">接口地址</param>
        /// <param name="args">接口参数</param>
        /// <returns></returns>
        public static async Task<string> PostAsync(string url, Dictionary<string, string> args)
        {
            using var httpClient = new HttpClient();
            httpClient.Timeout = new TimeSpan(0, 0, 60);
            HttpContent content = new FormUrlEncodedContent(args);
            var result = httpClient.PostAsync(url, content).Result.Content.ReadAsStringAsync().Result;
            return result;
        }
        #endregion
    }
}
