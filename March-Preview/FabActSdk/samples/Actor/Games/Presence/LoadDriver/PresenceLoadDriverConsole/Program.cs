﻿//-----------------------------------------------------------------------
//  Copyright (c) Microsoft Corporation. All rights reserved.
// 
//      THIS CODE AND INFORMATION ARE PROVIDED "AS IS" WITHOUT WARRANTY OF ANY KIND, 
//      EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE IMPLIED WARRANTIES 
//      OF MERCHANTABILITY AND/OR FITNESS FOR A PARTICULAR PURPOSE.
// ----------------------------------------------------------------------------------
//      The example companies, organizations, products, domain names,
//      e-mail addresses, logos, people, places, and events depicted
//      herein are fictitious.  No association with any real company,
//      organization, product, domain name, email address, logo, person,
//      places, or events is intended or should be inferred.
//-----------------------------------------------------------------------

namespace Microsoft.Fabric.Actor.Samples
{
    using System;
    using System.Collections.Generic;
    using System.Fabric.Services;
    using System.Fabric.Services.Wcf;
    using System.Globalization;
    using System.Linq;
    using System.ServiceModel;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    
    class Program
    {
        static int Main(string[] args)
        {
            var parsedArguments = new Arguments();
            if (!CommandLineUtility.ParseCommandLineArguments(args, parsedArguments))
            {
                Console.Write(CommandLineUtility.CommandLineArgumentsUsage(typeof(Arguments)));
                return -1;
            }

            if (parsedArguments.Target == Target.Local)
            {
                RunLocal(parsedArguments);
            }
            if (parsedArguments.Target == Target.Remote)
            {
                RunRemote(parsedArguments);
            }

            return 0;
        }

        static void RunRemote(Arguments parsedArgs)
        {
            var drivers = CreateDriverClients(parsedArgs);
            PrimeAllDrivers(drivers);
            var resultList = ExecuteTestsOnAllDrivers(drivers, parsedArgs);
            ProcessAndPrintResults(resultList);
        }

        private static IPresenceLoadDriver[] CreateDriverClients(Arguments parsedArgs)
        {
            var servicePartitionResolver = ServicePartitionResolver.GetDefault();
            var resolveTask = servicePartitionResolver.ResolveAsync(new Uri(parsedArgs.DriverServiceUri), null, CancellationToken.None);
            var rsp = resolveTask.Result;
            if (rsp.Endpoints.Count != parsedArgs.NumDrivers)
            {
                throw new ApplicationException(
                    string.Format(CultureInfo.InvariantCulture,
                    "Could not resolve one or more driver instance of service {0}, found {1}, expected {2}.",
                    parsedArgs.DriverServiceUri, 
                    rsp.Endpoints.Count, 
                    parsedArgs.NumDrivers));
            }

            // create clients
            var drivers = new IPresenceLoadDriver[parsedArgs.NumDrivers];
            var endpoints = rsp.Endpoints.ToArray();
            for(var i = 0; i < drivers.Length; i++)
            {
                drivers[i] = ChannelFactory<IPresenceLoadDriver>.CreateChannel(
                    WcfUtil.DefaultTcpClientBinding, 
                    new EndpointAddress(endpoints[i].Address));
            }

            return drivers;
        }

        private static void PrimeAllDrivers(IPresenceLoadDriver[] drivers)
        {
            var tasks = new List<Task>();
            for (var i = 0; i < drivers.Length; i++)
            {
                tasks.Add(drivers[i].Ping());
                Console.WriteLine(@"Connected to driver {0} at {1}", i, ((IClientChannel)drivers[i]).RemoteAddress);
            }

            Task.WaitAll(tasks.ToArray());
        }

        private static IList<TestResults> ExecuteTestsOnAllDrivers(IList<IPresenceLoadDriver> drivers, Arguments parsedArgs)
        {
            var specification = CreateTestSpecifications(parsedArgs);
            var tasks = new List<Task<TestResults>>();
            for (int i = 0; i < parsedArgs.NumDrivers; i++)
            {
                tasks.Add(ExecuteTestOnDriver(drivers[i], i, specification));
            }

            var retval = new List<TestResults>();
            foreach (var t in tasks)
            {
                t.Wait();
                retval.Add(t.Result);
            }

            return retval;
        }

        private async static Task<TestResults> ExecuteTestOnDriver(IPresenceLoadDriver driver, int driverId, TestSpecifications specifications)
        {
            var result = await driver.RunTest(driverId, specifications);
            Console.WriteLine("Driver: {0} finished.", driverId);

            return result;
        }

        static void RunLocal(Arguments parsedArgs)
        {
            var specification = CreateTestSpecifications(parsedArgs);
            var result = TestExecutor.RunTest(specification).Result;
            var resultList = new List<TestResults> { result };
            ProcessAndPrintResults(resultList);
        }

        static TestSpecifications CreateTestSpecifications(Arguments parsedArguments)
        {
            return new TestSpecifications
            {
                ApplicationName = parsedArguments.ActorApplicationName,
                NumThreads = parsedArguments.NumThreadsPerDriver,
                NumActorsPerThread = parsedArguments.NumActorsPerThread,
                NumOperationsPerActor = parsedArguments.NumOperationsPerActor,
                ReadToWriteRatio = parsedArguments.ReadToWriteRatio,
                NumOutstandingOperationsPerThread = parsedArguments.NumOutstandingOperations
            };
        }

        private static void ProcessAndPrintResults(ICollection<TestResults> results)
        {
            long maxElaspedWallClockMillis = long.MinValue;
            long minElaspedWallClockMillis = long.MaxValue;
            long avgElaspedWallClockMillis = 0;

            long totalReadOperationsInSystem = 0;
            long totalWriteOperationsInSystem = 0;

            Console.WriteLine(
                "Id, TotalReads, TotalWrites, TotalElaspedMills, MinReadMillis, MaxReadMillis, MinWriteMillis, MaxWriteMillis");
            foreach (var result in results)
            {
                Console.WriteLine(ToString(result));

                avgElaspedWallClockMillis += result.TotalElaspedWallClockMillis;
                if (result.TotalElaspedWallClockMillis > maxElaspedWallClockMillis)
                {
                    maxElaspedWallClockMillis = result.TotalElaspedWallClockMillis;
                }
                if (result.TotalElaspedWallClockMillis < minElaspedWallClockMillis)
                {
                    minElaspedWallClockMillis = result.TotalElaspedWallClockMillis;
                }

                totalReadOperationsInSystem += result.ReadOperationStats.TotalOperationsPerformed;
                totalWriteOperationsInSystem += result.WriteOperationStats.TotalOperationsPerformed;
            }

            avgElaspedWallClockMillis = ((1L * avgElaspedWallClockMillis) / results.Count);

            Console.WriteLine();
            Console.WriteLine("Total Operations: = {0}", totalReadOperationsInSystem + totalWriteOperationsInSystem);
            Console.WriteLine("\tTotal Reads        = {0}", totalReadOperationsInSystem);
            Console.WriteLine("\tTotal Writes       = {0}", totalWriteOperationsInSystem);
            Console.WriteLine("\tPercentage Reads   = {0}", (100.0 * totalReadOperationsInSystem) / (totalWriteOperationsInSystem + totalReadOperationsInSystem));
            Console.WriteLine();
            Console.WriteLine("Max Elasped Time Wall Clock: = {0} milliseconds", maxElaspedWallClockMillis);
            Console.WriteLine("Min Elasped Time Wall Clock: = {0} milliseconds", minElaspedWallClockMillis);
            Console.WriteLine("Avg Elasped Time Wall Clock: = {0} milliseconds", avgElaspedWallClockMillis);
            Console.WriteLine();

            Console.WriteLine("Max Throughput: = {0} op/seconds",
                ((1000.00) * (totalReadOperationsInSystem + totalWriteOperationsInSystem)) / minElaspedWallClockMillis);

            Console.WriteLine("Min Throughput: = {0} op/seconds",
                ((1000.00) * (totalReadOperationsInSystem + totalWriteOperationsInSystem)) / maxElaspedWallClockMillis);

            Console.WriteLine("Avg Throughput: = {0} op/seconds",
                ((1000.00) * (totalReadOperationsInSystem + totalWriteOperationsInSystem)) / avgElaspedWallClockMillis);
        }

        private static string ToString(TestResults result)
        {
            var sb = new StringBuilder();
            sb.AppendFormat("{0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}",
                result.DriverId,
                result.ReadOperationStats.TotalOperationsPerformed,
                result.WriteOperationStats.TotalOperationsPerformed,
                result.TotalElaspedWallClockMillis,
                result.ReadOperationStats.MinElaspedTimeMillis,
                result.ReadOperationStats.MaxElaspedTimeMillis,
                result.WriteOperationStats.MinElaspedTimeMillis,
                result.WriteOperationStats.MaxElaspedTimeMillis);
            return sb.ToString();
        }
    }

    class Arguments
    {
        [CommandLineArgument(
            CommandLineArgumentType.AtMostOnce,
            Description = "Run the test locally or send commands to the driver actors.",
            LongName = "target",
            ShortName = "t")]
        public Target Target = Target.Local;

        [CommandLineArgument(
            CommandLineArgumentType.AtMostOnce,
            Description = "Number of drivers, this is only applicable to remote target, default is 1",
            LongName = "drivers",
            ShortName = "d")]
        public int NumDrivers = 1;

        [CommandLineArgument(
           CommandLineArgumentType.AtMostOnce,
           Description = "Number of threads per driver, default is 1",
           LongName = "threads",
           ShortName = "h")]
        public int NumThreadsPerDriver = 1;

        [CommandLineArgument(
           CommandLineArgumentType.AtMostOnce,
           Description = "Number of actors per thread, default is 100.",
           LongName = "actors",
           ShortName = "a")]
        public int NumActorsPerThread = 100;

        [CommandLineArgument(
           CommandLineArgumentType.AtMostOnce,
           Description = "Number of actors per thread, default is 10.",
           LongName = "operations",
           ShortName = "o")]
        public int NumOperationsPerActor = 10;

        [CommandLineArgument(
          CommandLineArgumentType.AtMostOnce,
          Description = "Number of outstanding operations per thread at a time, default is 100",
          LongName = "outstandingOperations",
          ShortName = "s")]
        public int NumOutstandingOperations = 100;

        [CommandLineArgument(
           CommandLineArgumentType.AtMostOnce,
           Description = "Read/(Write+Read) operation ratio, default is 0.7",
           LongName = "ratio",
           ShortName = "r")]
        public double ReadToWriteRatio = 0.7;

        [CommandLineArgument(
            CommandLineArgumentType.AtMostOnce,
            Description = "Uri of the actor application. The default is fabric:/PresenceActorApp.",
            LongName = "actorApplicationName",
            ShortName = "n")]
        public string ActorApplicationName = "fabric:/PresenceActorApp";

        [CommandLineArgument(
            CommandLineArgumentType.AtMostOnce,
            Description = "Uri of the driver service. The default is fabric:/PresenceLoadDriverApp/PresenceLoadDriverService.",
            LongName = "driverServiceUri",
            ShortName = "u")]
        public string DriverServiceUri = "fabric:/PresenceLoadDriverApp/PresenceLoadDriverService";
    }

    enum Target
    {
        Local,
        Remote
    }
}