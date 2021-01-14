using CommandLine;
using CommandLine.Text;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace FM.LiveSwitch.Mux
{
    internal static class Program
    {
        static void Main(string[] args)
        {
            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddConsole(options =>
                {
                    // reserve stdout for programmatic use
                    options.LogToStandardErrorThreshold = LogLevel.Trace;
                });
            });

            JsonConvert.DefaultSettings = () => new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                ContractResolver = new DefaultContractResolver
                {
                    NamingStrategy = new CamelCaseNamingStrategy()
                }
            };

            using var parser = new Parser((settings) =>
            {
                settings.CaseInsensitiveEnumValues = true;
                settings.HelpWriter = null;
            });

            var result = parser.ParseArguments<MuxOptions>(AppendEnvironmentVariables(args));

            result.WithParsed(options =>
            {
                Task.Run(async () =>
                {
                    await new Muxer(options, loggerFactory).Run();
                }).GetAwaiter().GetResult();
            });

            result.WithNotParsed(errors =>
            {
                var helpText = HelpText.AutoBuild(result, 96);
                helpText.Copyright = "Copyright (C) 2019 Frozen Mountain Software Ltd.";
                helpText.AddEnumValuesToHelpText = true;
                helpText.AddOptions(result);
                Console.Error.Write(helpText);
                Environment.Exit(1);
            });
        }

        private static string[] AppendEnvironmentVariables(string[] args)
        {
            if (args.Length == 0)
            {
                return args;
            }

            var verb = args[0];
            if (!TryGetOptions(verb, out var options))
            {
                return args;
            }

            var newArgs = new List<string>(args);
            foreach (var unusedOption in FilterOptions(args, options))
            {
                var value = Environment.GetEnvironmentVariable($"{Assembly.GetExecutingAssembly().GetName().Name.ToUpperInvariant()}_{verb.ToUpperInvariant()}_{unusedOption.LongName.ToUpperInvariant()}");
                if (value != null)
                {
                    Console.Error.WriteLine($"Environment variable discovered matching --{unusedOption.LongName} option.");
                    newArgs.Add($"--{unusedOption.LongName}={value}");
                }
            }
            return newArgs.ToArray();
        }

        private static string Environmentalize(string name)
        {
            return name.ToUpperInvariant().Replace("-", "_").Replace(".", "_");
        }

        private static bool TryGetOptions(string verb, out OptionAttribute[] options)
        {
            foreach (var type in Assembly.GetExecutingAssembly().GetTypes().Where(type => !type.IsAbstract))
            {
                var verbAttribute = type.GetCustomAttributes<VerbAttribute>().FirstOrDefault();
                if (verbAttribute != null)
                {
                    if (verbAttribute.Name == verb)
                    {
                        options = type.GetProperties()
                            .Select(property => property.GetCustomAttributes<OptionAttribute>().FirstOrDefault())
                            .Where(option => option != null).ToArray();
                        return true;
                    }
                }
            }

            options = null;
            return false;
        }

        private static OptionAttribute[] FilterOptions(string[] args, OptionAttribute[] options)
        {
            var usedLongNames = new HashSet<string>();
            var usedShortNames = new HashSet<string>();

            foreach (var arg in args)
            {
                if (arg.StartsWith("--"))
                {
                    var longName = arg.Substring(2);
                    if (longName.Contains('='))
                    {
                        longName = longName.Substring(0, longName.IndexOf('='));
                    }
                    usedLongNames.Add(longName);
                }
                else if (arg.StartsWith("-"))
                {
                    var shortName = arg.Substring(1);
                    if (shortName.Contains('='))
                    {
                        shortName = shortName.Substring(0, shortName.IndexOf('='));
                    }
                    usedShortNames.Add(shortName);
                }
            }

            return options.Where(option => !usedLongNames.Contains(option.LongName) && !usedShortNames.Contains(option.ShortName)).ToArray();
        }
    }
}
