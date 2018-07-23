﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Test.Utilities;
using Microsoft.Win32;
using Roslyn.Utilities;
using Xunit;

namespace Roslyn.Test.Utilities
{
    /// <summary>
    /// Container for common skip reasons. Secondary benefit allows us to use find all ref to 
    /// discover the set of tests affected by a particular scenario.
    /// </summary>
    public static class ConditionalSkipReason
    {
        public const string NoPiaNeedsDesktop = "NoPia is only supported on desktop";
        public const string NetModulesNeedDesktop = "Net Modules are only supported on desktop";
        public const string RestrictedTypesNeedDesktop = "Restricted types are only supported on desktop";
        public const string TestExecutionNeedsDesktopTypes = "Test execution depends on desktop types";
        public const string NativePdbRequiresDesktop = "Native PDB tests can only execute on windows desktop";
    }

    public class ConditionalFactAttribute : FactAttribute
    {
        /// <summary>
        /// This proprety exists to prevent users of ConditionalFact from accidentally putting documentation
        /// in the Skip proprety instead of Reason. Putting it into Skip would cause the test to be unconditionally
        /// skipped vs. conditionally skipped which is the entire point of this attribute.
        /// </summary>
        [Obsolete("ConditionalFact should use Reason or AlwaysSkip", error: true)]
        public new string Skip
        {
            get { return base.Skip; }
            set { base.Skip = value; }
        }

        /// <summary>
        /// Used to unconditionally Skip a test. For the rare occasion when a conditional test needs to be 
        /// unconditionally skipped (typically short term for a bug to be fixed).
        /// </summary>
        public string AlwaysSkip
        {
            get { return base.Skip; }
            set { base.Skip = value; }
        }

        public string Reason { get; set; }

        public ConditionalFactAttribute(params Type[] skipConditions)
        {
            foreach (var skipCondition in skipConditions)
            {
                ExecutionCondition condition = (ExecutionCondition)Activator.CreateInstance(skipCondition);
                if (condition.ShouldSkip)
                {
                    base.Skip = Reason ?? condition.SkipReason;
                    break;
                }
            }
        }
    }

    public abstract class ExecutionCondition
    {
        public abstract bool ShouldSkip { get; }
        public abstract string SkipReason { get; }
    }

    public static class ExecutionConditionUtil
    {
        public static bool IsWindows => Path.DirectorySeparatorChar == '\\';
        public static bool IsDesktop => CoreClrShim.AssemblyLoadContext.Type == null;
    }

    public class x86 : ExecutionCondition
    {
        public override bool ShouldSkip => IntPtr.Size != 4;

        public override string SkipReason => "Target platform is not x86";
    }

    public class HasShiftJisDefaultEncoding : ExecutionCondition
    {
        public override bool ShouldSkip => Encoding.GetEncoding(0)?.CodePage != 932;

        public override string SkipReason => "OS default codepage is not Shift-JIS (932).";
    }

    public class HasEnglishDefaultEncoding : ExecutionCondition
    {
        public override bool ShouldSkip => Encoding.GetEncoding(0)?.CodePage != 1252;

        public override string SkipReason => "OS default codepage is not Windows-1252.";
    }

    public class IsEnglishLocal : ExecutionCondition
    {
        public override bool ShouldSkip =>
            !CultureInfo.CurrentUICulture.Name.StartsWith("en", StringComparison.OrdinalIgnoreCase) ||
            !CultureInfo.CurrentCulture.Name.StartsWith("en", StringComparison.OrdinalIgnoreCase);

        public override string SkipReason => "Current culture is not en";
    }

    public class IsRelease : ExecutionCondition
    {
#if DEBUG
        public override bool ShouldSkip => true;
#else
        public override bool ShouldSkip => false;
#endif

        public override string SkipReason => "Test not supported in DEBUG";
    }

    public class WindowsOnly : ExecutionCondition
    {
        public override bool ShouldSkip => !ExecutionConditionUtil.IsWindows;
        public override string SkipReason => "Test not supported on Mac and Linux";
    }

    public class WindowsDesktopOnly : ExecutionCondition
    {
        public override bool ShouldSkip => !(ExecutionConditionUtil.IsWindows && ExecutionConditionUtil.IsDesktop);
        public override string SkipReason => "Test only supported on Windows desktop";
    }

    public class UnixLikeOnly : ExecutionCondition
    {
        public override bool ShouldSkip => !PathUtilities.IsUnixLikePlatform;
        public override string SkipReason => "Test not supported on Windows";
    }

    public class ClrOnly : ExecutionCondition
    {
        public override bool ShouldSkip => MonoHelpers.IsRunningOnMono();
        public override string SkipReason => "Test not supported on Mono";
    }

    public class DesktopOnly : ExecutionCondition
    {
        public override bool ShouldSkip => !ExecutionConditionUtil.IsDesktop;
        public override string SkipReason => "Test not supported on CoreCLR";
    }

    public class NoIOperationValidation : ExecutionCondition
    {
        public override bool ShouldSkip => !CompilationExtensions.EnableVerifyIOperation;
        public override string SkipReason => "Test not supported in TEST_IOPERATION_INTERFACE";
    }

    public class OSVersionWin8 : ExecutionCondition
    {
        public override bool ShouldSkip => !OSVersion.IsWin8;
        public override string SkipReason => "Window Version is not at least Win8 (build:9200)";
    }

    public class Framework35Installed : ExecutionCondition
    {
        public override bool ShouldSkip
        {
            get
            {
#if NET46
                try
                {
                    const string RegistryPath = @"Software\Microsoft\NET Framework Setup\NDP\v3.5";
                    var key = Registry.LocalMachine.OpenSubKey(RegistryPath);
                    if (key == null)
                    {
                        return true;
                    }

                    var value = Convert.ToInt32(key.GetValue("Install", 0) ?? 0);
                    return value == 0;
                }
                catch
                {
                    return true;
                }
#else
                return false;
#endif
            }
        }

        public override string SkipReason => ".NET Framework 3.5 is not installed";
    }

    public class NotFramework45 : ExecutionCondition
    {
        public override bool ShouldSkip
        {
            get
            {
                // On Framework 4.5, ExtensionAttribute lives in mscorlib...
                return typeof(System.Runtime.CompilerServices.ExtensionAttribute).GetTypeInfo().Assembly ==
                    typeof(object).GetTypeInfo().Assembly;
            }
        }

        public override string SkipReason => "Test currently not supported on Framework 4.5";
    }
}
