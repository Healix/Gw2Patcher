using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gw2Patcher.Util
{
    static class FileName
    {
        public static string FromAssetRequest(string request)
        {
            if (request.StartsWith("/program/101/1/"))
                request = request.Substring(14);

            return request.Replace('/', '_').Replace('\\', '_').Trim('_');
        }
    }
}
