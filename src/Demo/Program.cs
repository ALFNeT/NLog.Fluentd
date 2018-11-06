// NLog.Targets.Fluentd
// 
// Copyright (c) 2014 Moriyoshi Koizumi and contributors.
// 
// This file is licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//    http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using Newtonsoft.Json;

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
            // LogManager.ThrowExceptions = true;
            var logger = NLog.LogManager.GetLogger("demo");

            var testObj = new TestObject
            {
                A = "A",
                B = 2,
                D = DateTime.UtcNow,
                I = new InternalTestObject
                {
                    AA = "A",
                    BB = 2,
                    DD = DateTime.UtcNow
                }
            };
            var eventJson = JsonConvert.SerializeObject(testObj);
            while (true)
            {
                var i = 0;
                var watch = System.Diagnostics.Stopwatch.StartNew();
                while (i < 100000)
                {
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
