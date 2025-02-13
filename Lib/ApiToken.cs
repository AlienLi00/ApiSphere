using Public.DB;

namespace AS.Lib
{
    public class ApiToken
    {
        /// <summary>
        /// 生成新token
        /// </summary>
        /// <param name="cUserId"></param>
        /// <returns></returns>
        public static String AddToken(String cUserId)
        {
            var token = Guid.NewGuid().ToString();
            var dao = Tools.GetSysDAO();
            var sql = "Insert Into T_Token(GUID, cUserId, dLogin, dLast) Values(@GUID,@cUserId,GETDATE(),GETDATE())";
            var qpc = new QueryParameterCollection();
            qpc.Add("GUID", token);
            qpc.Add("cUserId", cUserId);
            dao.ExecuteNonQuery(sql, qpc, null);
            return token;
        }

        /// <summary>
        /// 检查token,有效返回true,无效返回false
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        public static Boolean CheckToken(String cToken)
        {
            var dao = Tools.GetSysDAO();
            //超过10分钟不执行的token进行删除
            var sql = "Delete T_Token Where DATEDIFF(s,dLast,GETDATE())/60 >=10;Select Count(1) From T_Token Where GUID=@cToken";
            var qpc = new QueryParameterCollection();
            qpc.Add("cToken", cToken);
            return Convert.ToInt32(dao.ExecuteScalar(sql, qpc, null)) > 0;
        }

        /// <summary>
        /// 更新token
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        public static void UpdateToken(String cToken)
        {
            var dao = Tools.GetSysDAO();
            //超过10分钟不执行的token进行删除
            var sql = "Update T_Token Set dLast=GetDate() Where GUID=@cToken";
            var qpc = new QueryParameterCollection();
            qpc.Add("cToken", cToken);
            dao.ExecuteNonQuery(sql, qpc, null);
        }
    }
}
