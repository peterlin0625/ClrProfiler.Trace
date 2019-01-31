﻿using System;
using System.Reflection;
using System.Threading.Tasks;

namespace ClrProfiler.Trace.Utils
{
    public delegate void AsyncMethodEndDelegate(TraceMethodInfo traceMethodInfo, object returnValue, Exception ex);

    public static class DelegateHelper
    {
        private static readonly TaskCanceledException CanceledException = new TaskCanceledException();

        public static void AsyncMethodEnd(AsyncMethodEndDelegate endMethodDelegate, 
            TraceMethodInfo traceMethodInfo, 
            Exception ex,
            object returnValue)
        {
            if (ex != null)
            {
                endMethodDelegate(traceMethodInfo, null, ex);
                return;
            }

            if (returnValue != null)
            {
                var tcs = new TaskCompletionSource<dynamic>();
                ((Task)returnValue).ContinueWith(n =>
                {
                    if (n.IsFaulted)
                    {
                        var ex0 = n.Exception?.InnerException ?? new Exception("unknown exception");
                        endMethodDelegate(traceMethodInfo, null, ex0);
                        tcs.SetException(ex0);
                    }
                    else
                    {
                        if (n.IsCanceled)
                        {
                            endMethodDelegate(traceMethodInfo, null, CanceledException);
                            tcs.SetCanceled();
                        }
                        else
                        {
                            var methodInfo = traceMethodInfo.MethodBase as MethodInfo;
                            if (methodInfo != null && methodInfo.ReturnType == typeof(Task))
                            {
                                endMethodDelegate(traceMethodInfo, null, null);
                                tcs.SetResult(1);
                            }
                            else
                            {
                                var ret = ((dynamic)n).Result;
                                endMethodDelegate(traceMethodInfo, ret, null);
                                tcs.SetResult(ret);
                            }
                        }
                    }
                }, TaskScheduler.Default);
                returnValue = tcs.Task;
            }
        }
    }
}