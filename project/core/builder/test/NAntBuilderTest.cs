using System;
using System.ComponentModel;
using System.IO;
using Exortech.NetReflector;
using NMock;
using NMock.Constraints;
using NUnit.Framework;
using ThoughtWorks.CruiseControl.Core.Util;
using ThoughtWorks.CruiseControl.Remote;

namespace ThoughtWorks.CruiseControl.Core.Builder.Test
{
	[TestFixture]
	public class NAntBuilderTest : CustomAssertion
	{
		public const int SUCCESSFUL_EXIT_CODE = 0;
		public const int FAILED_EXIT_CODE = -1;

		private NAntBuilder _builder;
		private IMock _mockExecutor;

		[SetUp]
		public void SetUp()
		{
			_mockExecutor = new DynamicMock(typeof (ProcessExecutor));
			_builder = new NAntBuilder((ProcessExecutor) _mockExecutor.MockInstance);
		}

		private void VerifyAll()
		{
			_mockExecutor.Verify();
		}

		[TearDown]
		public void TearDown()
		{
			_mockExecutor.Verify();
		}

		[Test]
		public void PopulateFromReflector()
		{
			string xml = @"
    <nant>
    	<executable>NAnt.exe</executable>
    	<baseDirectory>C:\</baseDirectory>
    	<buildFile>mybuild.build</buildFile>
		<targetList>
      		<target>foo</target>
    	</targetList>
		<logger>SourceForge.NAnt.XmlLogger</logger>
		<buildTimeoutSeconds>123</buildTimeoutSeconds>
    </nant>";

			NetReflector.Read(xml, _builder);
			Assert.AreEqual(@"C:\", _builder.ConfiguredBaseDirectory);
			Assert.AreEqual("mybuild.build", _builder.BuildFile);
			Assert.AreEqual("NAnt.exe", _builder.Executable);
			Assert.AreEqual(1, _builder.Targets.Length);
			Assert.AreEqual(123, _builder.BuildTimeoutSeconds);
			Assert.AreEqual("SourceForge.NAnt.XmlLogger", _builder.Logger);
			Assert.AreEqual("foo", _builder.Targets[0]);
		}

		[Test]
		public void PopulateFromConfigurationUsingOnlyRequiredElementsAndCheckDefaultValues()
		{
			string xml = @"<nant />";

			NetReflector.Read(xml, _builder);
			Assert.AreEqual(null, _builder.ConfiguredBaseDirectory);
			Assert.AreEqual(NAntBuilder.DEFAULT_EXECUTABLE, _builder.Executable);
			Assert.AreEqual(0, _builder.Targets.Length);
			Assert.AreEqual(NAntBuilder.DEFAULT_BUILD_TIMEOUT, _builder.BuildTimeoutSeconds);
			Assert.AreEqual(NAntBuilder.DEFAULT_LOGGER, _builder.Logger);
		}

		[Test]
		public void ShouldSetSuccessfulStatusAndBuildOutputAsAResultOfASuccessfulBuild()
		{
			ProcessResult returnVal = CreateSuccessfulProcessResult();
			_mockExecutor.ExpectAndReturn("Execute", returnVal, new IsAnything());

			IntegrationResult result = new IntegrationResult();
			_builder.Run(result);

			Assert.IsTrue(result.Succeeded);
			Assert.AreEqual(IntegrationStatus.Success, result.Status);
			Assert.AreEqual(returnVal.StandardOutput, result.Output);
		}

		[Test]
		public void ShouldSetFailedStatusAndBuildOutputAsAResultOfFailedBuild()
		{
			ProcessResult returnVal = new ProcessResult("output", null, FAILED_EXIT_CODE, false);
			_mockExecutor.ExpectAndReturn("Execute", returnVal, new IsAnything());

			IntegrationResult result = new IntegrationResult();
			_builder.Run(result);

			Assert.IsTrue(result.Failed);
			Assert.AreEqual(IntegrationStatus.Failure, result.Status);
			Assert.AreEqual(returnVal.StandardOutput, result.Output);
		}

		[Test, ExpectedException(typeof (BuilderException))]
		public void ShouldThrowBuilderExceptionIfProcessTimesOut()
		{
			ProcessResult returnVal = new ProcessResult("output", null, SUCCESSFUL_EXIT_CODE, true);
			_mockExecutor.ExpectAndReturn("Execute", returnVal, new IsAnything());

			IntegrationResult result = new IntegrationResult();
			_builder.Run(result);
		}

		[Test, ExpectedException(typeof (BuilderException))]
		public void ShouldThrowBuilderExceptionIfProcessThrowsException()
		{
			_mockExecutor.ExpectAndThrow("Execute", new Win32Exception(), new IsAnything());

			IntegrationResult result = new IntegrationResult();
			_builder.Run(result);
		}

		[Test]
		public void ShouldPassSpecifiedPropertiesAsProcessInfoArgumentsToProcessExecutor()
		{
			ProcessResult returnVal = CreateSuccessfulProcessResult();
			CollectingConstraint constraint = new CollectingConstraint();
			_mockExecutor.ExpectAndReturn("Execute", returnVal, constraint);

			IntegrationResult result = new IntegrationResult();
			result.Label = "1.0";

			_builder.ConfiguredBaseDirectory = @"c:\";
			_builder.Executable = "NAnt.exe";
			_builder.BuildFile = "mybuild.build";
			_builder.BuildArgs = "myArgs";
			_builder.Targets = new string[] {"target1", "target2"};
			_builder.BuildTimeoutSeconds = 2;
			_builder.Run(result);

			ProcessInfo info = (ProcessInfo) constraint.Parameter;
			Assert.AreEqual(_builder.Executable, info.FileName);
			Assert.AreEqual(_builder.ConfiguredBaseDirectory, info.WorkingDirectory);
			Assert.AreEqual(2000, info.TimeOut);
			Assert.AreEqual(@"-buildfile:mybuild.build -logger:NAnt.Core.XmlLogger myArgs -D:label-to-apply=1.0 -D:ccnet.label=1.0 -D:ccnet.buildcondition=NoBuild target1 target2", info.Arguments);
		}

		[Test]
		public void ShouldPassAppropriateDefaultPropertiesAsProcessInfoArgumentsToProcessExecutor()
		{
			ProcessResult returnVal = CreateSuccessfulProcessResult();
			CollectingConstraint constraint = new CollectingConstraint();
			_mockExecutor.ExpectAndReturn("Execute", returnVal, constraint);

			_builder.Run(new IntegrationResult());

			ProcessInfo info = (ProcessInfo) constraint.Parameter;
			Assert.AreEqual(_builder.Executable, NAntBuilder.DEFAULT_EXECUTABLE);
			Assert.AreEqual(NAntBuilder.DEFAULT_BUILD_TIMEOUT*1000, info.TimeOut);
			Assert.AreEqual("-logger:NAnt.Core.XmlLogger -D:ccnet.buildcondition=NoBuild", info.Arguments);
		}

		[Test]
		public void ShouldPutQuotesAroundBuildFileIfItContainsASpace()
		{
			ProcessResult returnVal = CreateSuccessfulProcessResult();
			CollectingConstraint constraint = new CollectingConstraint();
			_mockExecutor.ExpectAndReturn("Execute", returnVal, constraint);

			_builder.BuildFile = "my project.build";
			_builder.Run(new IntegrationResult());

			ProcessInfo info = (ProcessInfo) constraint.Parameter;
			Assert.AreEqual(@"-buildfile:""my project.build"" -logger:NAnt.Core.XmlLogger -D:ccnet.buildcondition=NoBuild", info.Arguments);
		}

		[Test]
		public void IfConfiguredBaseDirectoryIsNotSetUseProjectWorkingDirectoryAsBaseDirectory()
		{
			_builder.ConfiguredBaseDirectory = null;
			CheckBaseDirectoryIsProjectDirectoryWithGivenRelativePart("");
		}

		[Test]
		public void IfConfiguredBaseDirectoryIsEmptyUseProjectWorkingDirectoryAsBaseDirectory()
		{
			_builder.ConfiguredBaseDirectory = "";
			CheckBaseDirectoryIsProjectDirectoryWithGivenRelativePart("");
		}

		[Test]
		public void IfConfiguredBaseDirectoryIsNotAbsoluteUseProjectWorkingDirectoryAsFirstPartOfBaseDirectory()
		{
			_builder.ConfiguredBaseDirectory = "relativeBaseDirectory";
			CheckBaseDirectoryIsProjectDirectoryWithGivenRelativePart("relativeBaseDirectory");
		}

		private void CheckBaseDirectoryIsProjectDirectoryWithGivenRelativePart(string relativeDirectory)
		{
			string expectedBaseDirectory = "projectWorkingDirectory";
			if (relativeDirectory.Length > 0)
			{
				expectedBaseDirectory = Path.Combine(expectedBaseDirectory, relativeDirectory);
			}
			CheckBaseDirectory(new IntegrationResult("project", "projectWorkingDirectory"), expectedBaseDirectory);
		}

		[Test]
		public void IfConfiguredBaseDirectoryIsAbsoluteUseItAtBaseDirectory()
		{
			_builder.ConfiguredBaseDirectory = @"c:\my\base\directory";
			CheckBaseDirectory(new IntegrationResult(), @"c:\my\base\directory");
		}

		private void CheckBaseDirectory(IntegrationResult result, string expectedBaseDirectory)
		{
			ProcessResult returnVal = CreateSuccessfulProcessResult();
			CollectingConstraint constraint = new CollectingConstraint();
			_mockExecutor.ExpectAndReturn("Execute", returnVal, constraint);

			_builder.Run(result);

			ProcessInfo info = (ProcessInfo) constraint.Parameter;
			Assert.AreEqual(expectedBaseDirectory, info.WorkingDirectory);
			VerifyAll();
		}

		[Test]
		public void ShouldRun()
		{
			AssertFalse(_builder.ShouldRun(new IntegrationResult()));
			Assert.IsTrue(_builder.ShouldRun(CreateIntegrationResultWithModifications(IntegrationStatus.Unknown)));
			Assert.IsTrue(_builder.ShouldRun(CreateIntegrationResultWithModifications(IntegrationStatus.Success)));
			AssertFalse(_builder.ShouldRun(CreateIntegrationResultWithModifications(IntegrationStatus.Failure)));
			AssertFalse(_builder.ShouldRun(CreateIntegrationResultWithModifications(IntegrationStatus.Exception)));
		}

		[Test]
		public void ShouldGiveAPresentationValueForTargetsThatIsANewLineSeparatedEquivalentOfAllTargets()
		{
			_builder.Targets = new string[] {"target1", "target2"};
			Assert.AreEqual("target1" + Environment.NewLine + "target2", _builder.TargetsForPresentation);
		}

		[Test]
		public void ShouldWorkForSingleTargetWhenSettingThroughPresentationValue()
		{
			_builder.TargetsForPresentation = "target1";
			Assert.AreEqual("target1", _builder.Targets[0]);
			Assert.AreEqual(1, _builder.Targets.Length);
		}

		[Test]
		public void ShouldSplitAtNewLineWhenSettingThroughPresentationValue()
		{
			_builder.TargetsForPresentation = "target1" + Environment.NewLine + "target2";
			Assert.AreEqual("target1", _builder.Targets[0]);
			Assert.AreEqual("target2", _builder.Targets[1]);
			Assert.AreEqual(2, _builder.Targets.Length);
		}

		[Test]
		public void ShouldWorkForEmptyAndNullStringsWhenSettingThroughPresentationValue()
		{
			_builder.TargetsForPresentation = "";
			Assert.AreEqual(0, _builder.Targets.Length);
			_builder.TargetsForPresentation = null;
			Assert.AreEqual(0, _builder.Targets.Length);
		}

		private ProcessResult CreateSuccessfulProcessResult()
		{
			return new ProcessResult("output", null, SUCCESSFUL_EXIT_CODE, false);
		}

		private IntegrationResult CreateIntegrationResultWithModifications(IntegrationStatus status)
		{
			IntegrationResult result = new IntegrationResult();
			result.BuildCondition = BuildCondition.ForceBuild;
			result.Status = status;
			result.Modifications = new Modification[] {new Modification()};
			return result;
		}
	}
}