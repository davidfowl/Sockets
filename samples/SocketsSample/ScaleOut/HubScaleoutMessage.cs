using System.IO;

namespace SocketsSample
{
    public class HubScaleoutMessage
    {
        public HubScaleoutMessage(string hubName, string method, object[] args)
        {
            HubName = hubName;
            Method = method;
            Args = args;
        }

        public string HubName { get; }

        public string Method { get; }

        public object[] Args { get; }
    }
}