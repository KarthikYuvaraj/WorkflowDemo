﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Fonlow.Activities;
using System.Activities;
using System.Activities.Expressions;
using System.Threading;
using System.Activities.DurableInstancing;
using System.Diagnostics;
using System.ServiceModel;
using System.ServiceModel.Activities;
using System.ServiceModel.Activities.Description;
using Fonlow.Activities.ServiceModel;
using System.ServiceModel.Description;
using System.Activities.Statements;

namespace BasicTests
{
    public class WorkflowServiceHostPersistenceTests
    {
        const string connectionString = "Server =localhost; Initial Catalog = WF; Integrated Security = SSPI";
        const string hostBaseAddress = "net.tcp://localhost/CountingService";
        [Fact]
        public void TestOpenHost()
        {
            // Create service host.
            using (WorkflowServiceHost host = new WorkflowServiceHost(new Microsoft.Samples.BuiltInConfiguration.CountingWorkflow(), new Uri(hostBaseAddress)))
            {

                // Add service endpoint.
                host.AddServiceEndpoint("ICountingWorkflow", new NetTcpBinding(), "");

                SqlWorkflowInstanceStoreBehavior instanceStoreBehavior = new SqlWorkflowInstanceStoreBehavior("Server =localhost; Initial Catalog = WF; Integrated Security = SSPI")
                {
                    HostLockRenewalPeriod = new TimeSpan(0, 0, 5),
                    RunnableInstancesDetectionPeriod = new TimeSpan(0, 0, 2),
                    InstanceCompletionAction = InstanceCompletionAction.DeleteAll,
                    InstanceLockedExceptionAction = InstanceLockedExceptionAction.AggressiveRetry,
                    InstanceEncodingOption = InstanceEncodingOption.GZip,
                };
                host.Description.Behaviors.Add(instanceStoreBehavior);

                host.Open();
                Assert.Equal(CommunicationState.Opened, host.State);


                // Create a client that sends a message to create an instance of the workflow.
                ICountingWorkflow client = ChannelFactory<ICountingWorkflow>.CreateChannel(new NetTcpBinding(), new EndpointAddress(hostBaseAddress));
                client.start();
                Debug.WriteLine("client.start() done.");
                System.Threading.Thread.Sleep(10000);
                Debug.WriteLine("sleep finished");
                host.Close();
            }
        }

        [Fact]
        public void TestOpenHostWithWrongStoreThrows()
        {
            // Create service host.
            WorkflowServiceHost host = new WorkflowServiceHost(new Microsoft.Samples.BuiltInConfiguration.CountingWorkflow(), new Uri(hostBaseAddress));


            // Add service endpoint.
            host.AddServiceEndpoint("ICountingWorkflow", new NetTcpBinding(), "");

            SqlWorkflowInstanceStoreBehavior instanceStoreBehavior = new SqlWorkflowInstanceStoreBehavior("Server =localhost; Initial Catalog = WFXXX; Integrated Security = SSPI")
            {
                HostLockRenewalPeriod = new TimeSpan(0, 0, 5),
                RunnableInstancesDetectionPeriod = new TimeSpan(0, 0, 2),
                InstanceCompletionAction = InstanceCompletionAction.DeleteAll,
                InstanceLockedExceptionAction = InstanceLockedExceptionAction.AggressiveRetry,
                InstanceEncodingOption = InstanceEncodingOption.GZip,

            };
            host.Description.Behaviors.Add(instanceStoreBehavior);


            var ex = Assert.Throws<CommunicationException>(()
                => host.Open());

            Assert.NotNull(ex.InnerException);
            Assert.Equal(typeof(System.Runtime.DurableInstancing.InstancePersistenceCommandException), ex.InnerException.GetType());
            Assert.Equal(CommunicationState.Faulted, host.State);//so can't be disposed.
        }


        [Fact]
        public void TestWaitForSignalOrDelay()
        {
            var uri = new Uri("net.tcp://localhost/");
            Guid id;
            // Create service host.
            using (var host = new ServiceHost(typeof(Fonlow.WorkflowDemo.Contracts.WakeupService), uri))
            {
                host.AddServiceEndpoint("Fonlow.WorkflowDemo.Contracts.IWakeup", new NetTcpBinding(), "wakeup");

                ServiceDebugBehavior debug = host.Description.Behaviors.Find<ServiceDebugBehavior>();

                if (debug == null)
                {
                    host.Description.Behaviors.Add(
                         new ServiceDebugBehavior() { IncludeExceptionDetailInFaults = true });
                }
                else
                {
                    // make sure setting is turned ON
                    if (!debug.IncludeExceptionDetailInFaults)
                    {
                        debug.IncludeExceptionDetailInFaults = true;
                    }
                }


                host.Open();
                Assert.Equal(CommunicationState.Opened, host.State);


                // Create a client that sends a message to create an instance of the workflow.
                var client = ChannelFactory<Fonlow.WorkflowDemo.Contracts.IWakeup>.CreateChannel(new NetTcpBinding(), new EndpointAddress(new Uri(uri, "wakeup")));
                id = client.Create("Service wakeup", TimeSpan.FromSeconds(100));
                Assert.NotEqual(Guid.Empty, id);

                Thread.Sleep(2000);//so the service may have time to persist.

                var ok = client.LoadAndRun(id);//Optional. Just simulate that the workflow instance may be running againt because of other events.
                Assert.True(ok);

                var r = client.Wakeup(id, "Service wakeup");
                Assert.Equal("Someone waked me up", r);

                host.Close();
            }
        }

        [Fact]
        public void TestWaitForSignalOrDelayAfterDelay()
        {
            var uri = new Uri("net.tcp://localhost/");
            Guid id;
            // Create service host.
            using (var host = new ServiceHost(typeof(Fonlow.WorkflowDemo.Contracts.WakeupService), uri))
            {
                host.AddServiceEndpoint("Fonlow.WorkflowDemo.Contracts.IWakeup", new NetTcpBinding(), "wakeup");

                ServiceDebugBehavior debug = host.Description.Behaviors.Find<ServiceDebugBehavior>();

                if (debug == null)
                {
                    host.Description.Behaviors.Add(
                         new ServiceDebugBehavior() { IncludeExceptionDetailInFaults = true });
                }
                else
                {
                    // make sure setting is turned ON
                    if (!debug.IncludeExceptionDetailInFaults)
                    {
                        debug.IncludeExceptionDetailInFaults = true;
                    }
                }


                host.Open();
                Assert.Equal(CommunicationState.Opened, host.State);


                // Create a client that sends a message to create an instance of the workflow.
                var client = ChannelFactory<Fonlow.WorkflowDemo.Contracts.IWakeup>.CreateChannel(new NetTcpBinding(), new EndpointAddress(new Uri(uri, "wakeup")));
                id = client.Create("Service wakeup", TimeSpan.FromSeconds(1));
                Assert.NotEqual(Guid.Empty, id);

                Thread.Sleep(2000);//so the service may have time to persist. Upon being reloaded, no bookmark calls needed.


                var ok = client.LoadAndRun(id);
                Assert.True(ok);
                Thread.Sleep(8000);//So the service may have saved the result.

                var r = client.Wakeup(id, "Service wakeup");
                Assert.Equal("I sleep for good duration", r);

                host.Close();
            }
        }


    }
}
