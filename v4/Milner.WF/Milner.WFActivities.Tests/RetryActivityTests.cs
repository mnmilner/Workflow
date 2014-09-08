using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Activities.Statements;
using System.Activities;
using Microsoft.VisualBasic.Activities;
using System.Activities.Expressions;

namespace Milner.WFActivities.Tests
{
    [TestClass]
    public class RetryActivityTests
    {
        [TestMethod]
        public void TestRetryToFail()
        {
            Throw testable = new Throw {  Exception = new InArgument<Exception>(env=> new ApplicationException("Application Error"))};

            int iterations = 2;
            var activity = CreateRetryAndVariables(testable, iterations);
            var outputs = WorkflowInvoker.Invoke(activity);

            Assert.IsNotNull(outputs["currentException"], "Current Exception was null");
            Assert.IsNotNull(outputs["finalException"], "Final Exception was null");

            Assert.IsInstanceOfType(outputs["currentException"],typeof(System.ApplicationException), "Current Exception was not the correct type");
            Assert.IsInstanceOfType(outputs["finalException"], typeof(System.ApplicationException), "Final Exception was not the correct type");

            Assert.AreEqual<int>(iterations, (int)outputs["totalTries"], "Incorrect number of tries");

        }

        [TestMethod]
        public void TestRetrySuccess()
        {
            WriteLine testable = new WriteLine { Text = "Successful output" };
            int iterations = 2;
            var activity = CreateRetryAndVariables(testable, iterations);
            var outputs = WorkflowInvoker.Invoke(activity);

            Assert.AreEqual<int>(0, (int)outputs["totalTries"], "Incorrect number of tries");
            Assert.IsNull(outputs["currentException"], "Current Exception is not null");
            Assert.IsNull(outputs["finalException"], "Final Exception is not null");


        }

        [TestMethod]
        public void TestRetryInitialFail()
        {
            int iterations = 2;

            var testable = new If{
                Condition = new VisualBasicValue<bool>("shouldFail"),
                Then = new Sequence
                {
                    Activities =
                    {
                        new Assign<bool>{
                            To = new VisualBasicReference<bool>("shouldFail"),
                            Value = new InArgument<bool>(false)},
                        new Throw {Exception = new InArgument<Exception>(env=> new ApplicationException("Failed"))}
                    }
                },
                Else = new WriteLine { Text = "Success"}
            };
            var activity = CreateRetryAndVariables(testable, iterations);
            var outputs = WorkflowInvoker.Invoke(activity);

            //expect 0 because the first retry succeeds so counter doesn't increment
            Assert.AreEqual<int>(0, (int)outputs["totalTries"], "Incorrect number of tries");
            Assert.IsNotNull(outputs["currentException"], "Current exception not correctly populated");
            Assert.IsInstanceOfType(outputs["currentException"], typeof(System.ApplicationException), "Current exception is not the correct type");
            Assert.IsNull(outputs["finalException"], "Final exception should be null");

        }

        private Activity CreateRetryAndVariables(Activity retryable, int iterations)
        {
            DelegateInArgument<Exception> currentExceptionArg = new DelegateInArgument<Exception>("exceptionArg");
            DelegateInArgument<int> currentTry = new DelegateInArgument<int>("currentTry");
            DelegateInArgument<Exception> finalExceptionArg = new DelegateInArgument<Exception>("finalExceptionArg");

            DynamicActivityProperty currentExceptionOutArg = new DynamicActivityProperty{
                Name = "currentException", 
                Type = typeof(OutArgument<System.Exception>)
                        
            };
            DynamicActivityProperty finalExceptionOutArg = new DynamicActivityProperty
            {
                Name = "finalException",
                Type = typeof(OutArgument<System.Exception>)
            };

            DynamicActivityProperty totalTries = new DynamicActivityProperty
            {
                Name = "totalTries",
                Type = typeof(OutArgument<int>)
            };

            DynamicActivity activity = new DynamicActivity
            {
                Properties =
                {
                    currentExceptionOutArg,
                    finalExceptionOutArg,
                    totalTries
                },
                Implementation = () =>
              new Sequence
              { 
                  Variables = {
                      new Variable<bool>("shouldFail", true)
                  },
                  Activities =
                 {
                    new RetryActivity{
                        ActivityToRetry = 
                        new ActivityAction { Handler= retryable}, 
                        retryCount = iterations,
                        retryInterval = TimeSpan.FromMilliseconds(100),
                        CurrentExceptionHandler = new ActivityAction<Exception,int>{
                            Argument1 = currentExceptionArg,
                            Argument2 = currentTry,
                            //Handler = new WriteLine{Text = "iterating"}
                            Handler = 
                                new Sequence{
                                    Activities = {
                                        new Assign<Exception> { 
                                            To = new ArgumentReference<Exception>{ ArgumentName="currentException"},
                                            Value = new InArgument<Exception>(currentExceptionArg)},
                                        new Assign<int>{
                                            To = new ArgumentReference<int> {ArgumentName = "totalTries"},
                                            Value = new InArgument<int>(currentTry)
                                        }
                                    }
                                }
                            
                        },
                        FinalExceptionHandler = new ActivityAction<Exception>{
                            Argument = finalExceptionArg,
                            Handler = new Assign<Exception> {
                                To = new ArgumentReference<Exception>{ ArgumentName="finalException"},
                                Value = new InArgument<Exception>(finalExceptionArg)

                            }
                        }
                    }
                }
              }
            };

            return activity;
        }
    }
}
