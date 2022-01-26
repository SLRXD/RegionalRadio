using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.IO;

namespace RegionalRadio
{
    public class LogHelper
    {
        //private static readonly log4net.ILog Logerror = log4net.LogManager.GetLogger("DEBUG");

        public static void WriteInfoLog(string info)
        {
            //Logerror.Info(info);
        }

        public static void WriteErrorLog(string info)
        {
            //Logerror.Error(info);
        }

        public static void WriteDebugLog(string info)
        {
            //Logerror.Debug(info);
        }

        public static void WriteDebugLog(Exception exception)
        {
            //Logerror.Debug(exception.Message, exception);
        }
    }
}
