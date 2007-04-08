using NMock.Constraints;
using NUnit.Framework;
using ThoughtWorks.CruiseControl.Core;
using ThoughtWorks.CruiseControl.Core.Queues;
using ThoughtWorks.CruiseControl.Remote;
using ThoughtWorks.CruiseControl.UnitTests.UnitTestUtils;

namespace ThoughtWorks.CruiseControl.UnitTests.Core
{
	[TestFixture]
	public class ProjectIntegratorTest : IntegrationFixture
	{
		private const string TestQueueName = "projectQueue";
		private LatchMock integrationTriggerMock;
		private LatchMock projectMock;
		private ProjectIntegrator integrator;
		private IntegrationQueueSet integrationQueues;
		private IIntegrationQueue integrationQueue;

		[SetUp]
		public void SetUp()
		{
			integrationTriggerMock = new LatchMock(typeof (ITrigger));
			integrationTriggerMock.Strict = true;
			projectMock = new LatchMock(typeof (IProject));
			projectMock.Strict = true;
			projectMock.SetupResult("Name", "project");
			projectMock.SetupResult("QueueName", TestQueueName);
			projectMock.SetupResult("QueuePriority", 0);
			projectMock.SetupResult("Triggers", integrationTriggerMock.MockInstance);

			integrationQueues = new IntegrationQueueSet();
			integrationQueues.Add(TestQueueName);
			integrationQueue = integrationQueues[TestQueueName];
			integrator = new ProjectIntegrator((IProject) projectMock.MockInstance, integrationQueue);
		}

		[TearDown]
		public void TearDown()
		{
			if (integrator != null)
			{
				integrator.Stop();
				integrator.WaitForExit();
			}
		}

		private void VerifyAll()
		{
			integrationTriggerMock.Verify();
			projectMock.Verify();
		}

		[Test]
		public void ShouldContinueRunningIfNotToldToStop()
		{
			integrationTriggerMock.SetupResultAndSignal("Fire", null);
			projectMock.ExpectNoCall("Integrate", typeof (IntegrationRequest));
			integrationTriggerMock.ExpectNoCall("IntegrationCompleted");

			integrator.Start();
			integrationTriggerMock.WaitForSignal();
			Assert.AreEqual(ProjectIntegratorState.Running, integrator.State);
			VerifyAll();
		}

		[Test]
		public void ShouldStopWhenStoppedExternally()
		{
			integrationTriggerMock.SetupResultAndSignal("Fire", null);
			projectMock.ExpectNoCall("NotifyPendingState");
			projectMock.ExpectNoCall("Integrate", typeof (IntegrationRequest));
			projectMock.ExpectNoCall("NotifySleepingState");
			integrationTriggerMock.ExpectNoCall("IntegrationCompleted");

			integrator.Start();
			integrationTriggerMock.WaitForSignal();
			Assert.AreEqual(ProjectIntegratorState.Running, integrator.State);

			integrator.Stop();
			integrator.WaitForExit();
			Assert.AreEqual(ProjectIntegratorState.Stopped, integrator.State);
			VerifyAll();
		}

		[Test]
		public void StartMultipleTimes()
		{
			integrationTriggerMock.SetupResultAndSignal("Fire", null);
			projectMock.ExpectNoCall("NotifyPendingState");
			projectMock.ExpectNoCall("Integrate", typeof (IntegrationRequest));
			projectMock.ExpectNoCall("NotifySleepingState");
			integrationTriggerMock.ExpectNoCall("IntegrationCompleted");

			integrator.Start();
			integrator.Start();
			integrator.Start();
			integrationTriggerMock.WaitForSignal();
			Assert.AreEqual(ProjectIntegratorState.Running, integrator.State);
			integrator.Stop();
			integrator.WaitForExit();
			Assert.AreEqual(ProjectIntegratorState.Stopped, integrator.State);
			VerifyAll();
		}

		[Test]
		public void RestartIntegrator()
		{
			integrationTriggerMock.SetupResultAndSignal("Fire", null);
			projectMock.ExpectNoCall("NotifyPendingState");
			projectMock.ExpectNoCall("Integrate", typeof (IntegrationRequest));
			projectMock.ExpectNoCall("NotifySleepingState");
			integrationTriggerMock.ExpectNoCall("IntegrationCompleted");

			integrator.Start();
			integrationTriggerMock.WaitForSignal();
			integrator.Stop();
			integrator.WaitForExit();

			integrationTriggerMock.ResetLatch();
			integrator.Start();
			integrationTriggerMock.WaitForSignal();
			integrator.Stop();
			integrator.WaitForExit();
			VerifyAll();
		}

		[Test]
		public void StopUnstartedIntegrator()
		{
			integrationTriggerMock.ExpectNoCall("Fire");
			projectMock.ExpectNoCall("NotifyPendingState");
			projectMock.ExpectNoCall("Integrate", typeof (IntegrationRequest));
			projectMock.ExpectNoCall("NotifySleepingState");
			integrationTriggerMock.ExpectNoCall("IntegrationCompleted");

			integrator.Stop();
			Assert.AreEqual(ProjectIntegratorState.Stopped, integrator.State);
			VerifyAll();
		}

		[Test]
		public void VerifyStateAfterException()
		{
			string exceptionMessage = "Intentional exception";

			integrationTriggerMock.ExpectAndReturn("Fire", ForceBuildRequest());
			projectMock.Expect("NotifyPendingState");
			projectMock.ExpectAndThrow("Integrate", new CruiseControlException(exceptionMessage), new HasForceBuildCondition());
			projectMock.ExpectAndSignal("NotifySleepingState");
			integrationTriggerMock.Expect("IntegrationCompleted");

			integrator.Start();
			projectMock.WaitForSignal();
			Assert.AreEqual(ProjectIntegratorState.Running, integrator.State);
			integrator.Stop();
			integrator.WaitForExit();
			Assert.AreEqual(ProjectIntegratorState.Stopped, integrator.State);
			VerifyAll();
		}

		[Test]
		public void Abort()
		{
			integrationTriggerMock.SetupResultAndSignal("Fire", null);
			projectMock.ExpectNoCall("NotifyPendingState");
			projectMock.ExpectNoCall("Integrate", typeof (IntegrationRequest));
			projectMock.ExpectNoCall("NotifySleepingState");
			integrationTriggerMock.ExpectNoCall("IntegrationCompleted");

			integrator.Start();
			integrationTriggerMock.WaitForSignal();
			Assert.AreEqual(ProjectIntegratorState.Running, integrator.State);
			integrator.Abort();
			integrator.WaitForExit();
			Assert.AreEqual(ProjectIntegratorState.Stopped, integrator.State);
			VerifyAll();
		}

		[Test]
		public void TerminateWhenProjectIsntStarted()
		{
			integrationTriggerMock.SetupResultAndSignal("Fire", null);
			projectMock.ExpectNoCall("NotifyPendingState");
			projectMock.ExpectNoCall("Integrate", typeof (IntegrationRequest));
			projectMock.ExpectNoCall("NotifySleepingState");
			integrationTriggerMock.ExpectNoCall("IntegrationCompleted");

			integrator.Abort();
			Assert.AreEqual(ProjectIntegratorState.Stopped, integrator.State);
			VerifyAll();
		}

		[Test]
		public void TerminateCalledTwice()
		{
			integrationTriggerMock.SetupResultAndSignal("Fire", null);
			projectMock.ExpectNoCall("NotifyPendingState");
			projectMock.ExpectNoCall("Integrate", typeof (IntegrationRequest));
			projectMock.ExpectNoCall("NotifySleepingState");
			integrationTriggerMock.ExpectNoCall("IntegrationCompleted");

			integrator.Start();
			integrationTriggerMock.WaitForSignal();
			Assert.AreEqual(ProjectIntegratorState.Running, integrator.State);
			integrator.Abort();
			integrator.Abort();
			VerifyAll();
		}

		[Test]
		public void ForceBuild()
		{
			integrationTriggerMock.ExpectNoCall("Fire");
			projectMock.Expect("Integrate", new HasForceBuildCondition());
			projectMock.Expect("NotifyPendingState");
			projectMock.ExpectAndSignal("NotifySleepingState");
			projectMock.ExpectNoCall("Integrate", typeof (IntegrationRequest));
			integrationTriggerMock.ExpectAndSignal("IntegrationCompleted");
			integrator.ForceBuild();
			integrationTriggerMock.WaitForSignal();
			projectMock.WaitForSignal();
			VerifyAll();
		}

		[Test]
		public void RequestIntegration()
		{
			IntegrationRequest request = new IntegrationRequest(BuildCondition.IfModificationExists, "intervalTrigger");
			projectMock.Expect("NotifyPendingState");
			projectMock.Expect("Integrate", request);
			projectMock.ExpectAndSignal("NotifySleepingState");
			integrationTriggerMock.ExpectAndSignal("IntegrationCompleted");
			integrator.Request(request);
			integrationTriggerMock.WaitForSignal();
			projectMock.WaitForSignal();
			Assert.AreEqual(ProjectIntegratorState.Running, integrator.State);
			VerifyAll();
		}

		[Test]
		public void ShouldClearRequestQueueAsSoonAsRequestIsProcessed()
		{
			IntegrationRequest request = new IntegrationRequest(BuildCondition.IfModificationExists, "intervalTrigger");
			projectMock.Expect("NotifyPendingState");
			projectMock.Expect("Integrate", request);
			projectMock.ExpectAndSignal("NotifySleepingState");
			integrationTriggerMock.Expect("IntegrationCompleted");
			integrationTriggerMock.ExpectAndReturnAndSignal("Fire", null);

			integrator.Request(request);
			projectMock.WaitForSignal();
			integrationTriggerMock.WaitForSignal();
			VerifyAll();
		}

		[Test]
		public void CancelPendingRequestDoesNothingForNoPendingItems()
		{
			int queuedItemCount = integrationQueue.GetQueuedIntegrations().Length;
			integrator.CancelPendingRequest();
			Assert.AreEqual(queuedItemCount, integrationQueue.GetQueuedIntegrations().Length);

			VerifyAll();
		}

		[Test]
		public void CancelPendingRequestRemovesPendingItems()
		{
			IProject project = (IProject) projectMock.MockInstance;

			IntegrationRequest request1 = new IntegrationRequest(BuildCondition.IfModificationExists, "intervalTrigger");
			projectMock.Expect("NotifyPendingState");
			integrationQueue.Enqueue(new IntegrationQueueItem(project, request1, integrator));

			IntegrationRequest request2 = new IntegrationRequest(BuildCondition.IfModificationExists, "intervalTrigger");
			projectMock.Expect("NotifyPendingState");
			integrationQueue.Enqueue(new IntegrationQueueItem(project, request2, integrator));

			int queuedItemCount = integrationQueue.GetQueuedIntegrations().Length;
			Assert.AreEqual(2, queuedItemCount);
			integrationTriggerMock.Expect("IntegrationCompleted");

			integrator.CancelPendingRequest();

			queuedItemCount = integrationQueue.GetQueuedIntegrations().Length;
			Assert.AreEqual(1, queuedItemCount);

			VerifyAll();
		}
	}

	public class HasForceBuildCondition : BaseConstraint
	{
		public override bool Eval(object val)
		{
			return ((IntegrationRequest) val).BuildCondition == BuildCondition.ForceBuild;
		}

		public override string Message
		{
			get { return "IntegrationRequest is not ForceBuild."; }
		}
	}
}