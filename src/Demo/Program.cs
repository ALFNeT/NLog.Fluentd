using System;
using Newtonsoft.Json;
using NLog;

namespace Demo
{
    class Program
    {
        public class InternalTestObject
        {
            public string AA { get; set; }
            public int BB { get; set; }

            public DateTime DD { get; set; }

            public override string ToString() { return AA; }
        }
        public class TestObject
        {
            public string A { get; set; }
            public int B { get; set; }

            public DateTime D { get; set; }

            public InternalTestObject I { get; set; }

            public override string ToString() { return A; }
        }
        static void Main(string[] args)
        {
            var logger = LogManager.GetLogger("demo");
            MappedDiagnosticsLogicalContext.Set("FluentdEnabled", "true");
            MappedDiagnosticsLogicalContext.Set("FluentdPort", "24224");
            MappedDiagnosticsLogicalContext.Set("FluendTimeout", "5000");
            while (true)
            {
                var i = 0;
                var watch = System.Diagnostics.Stopwatch.StartNew();
                while (i < 100)
                {
                    var testObj = new TestObject
                    {
                        A = "A",
                        B = i,
                        D = DateTime.UtcNow,
                        I = new InternalTestObject
                        {
                            AA = "A",
                            BB = i,
                            DD = DateTime.UtcNow
                        }
                    };
                    var eventJson = JsonConvert.SerializeObject(testObj);
                    logger.Info(eventJson);
                    i++;
                }
                watch.Stop();
                Console.Write("Elapsed time: {0}", watch.ElapsedMilliseconds);
                Console.ReadLine();
            }            
        }
    }
}
