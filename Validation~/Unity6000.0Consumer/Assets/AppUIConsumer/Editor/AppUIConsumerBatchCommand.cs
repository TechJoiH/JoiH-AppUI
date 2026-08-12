using System;
using UnityEditor;
using UnityEngine;

namespace Joi.H.AppUI.Validation.Consumer.Editor
{
    internal static class AppUIConsumerBatchCommand
    {
        public static void Run(Action command)
        {
            try
            {
                command.Invoke();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }
    }
}
