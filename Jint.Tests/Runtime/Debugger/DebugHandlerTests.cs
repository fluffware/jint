﻿using Jint.Native.Object;
using Jint.Runtime.Debugger;
using Xunit;

#pragma warning disable 618

namespace Jint.Tests.Runtime.Debugger
{
    public class DebugHandlerTests
    {
        [Fact]
        public void AvoidsPauseRecursion()
        {
            // While the DebugHandler is in a paused state, it shouldn't relay further OnStep calls to Break/Step.
            // Such calls would occur e.g. if Step/Break event handlers evaluate accessors. Failing to avoid
            // reentrance in a multithreaded environment (e.g. using ManualResetEvent(Slim)) would cause
            // a deadlock.
            string script = @"
                var obj = { get name() { 'fail'; return 'Smith'; } };
                'target';
            ";

            var engine = new Engine(options => options.DebugMode().InitialStepMode(StepMode.Into));

            bool didPropertyAccess = false;

            engine.DebugHandler.Step += (sender, info) =>
            {
                // We should never reach "fail", because the only way it's executed is from
                // within this Step handler
                Assert.False(info.ReachedLiteral("fail"));

                if (info.ReachedLiteral("target"))
                {
                    var obj = info.CurrentScopeChain.Global.GetBindingValue("obj") as ObjectInstance;
                    var prop = obj.GetOwnProperty("name");
                    // This is where reentrance would occur:
                    var value = prop.Get.Invoke(engine);
                    didPropertyAccess = true;
                }
                return StepMode.Into;
            };

            engine.Execute(script);

            Assert.True(didPropertyAccess);
        }
    }
}
