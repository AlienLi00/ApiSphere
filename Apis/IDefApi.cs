using System.Xml;

namespace AS.Apis
{
    /// <summary>
    /// 定义了自定义API应实现的方法，用于处理XML节点表示的任务。
    /// </summary>
    public interface IDefApi
    {
        public void DoApi(XmlNode taskXn);
    }
}
