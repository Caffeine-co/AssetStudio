using AssetStudioCore.Options;
using System;

namespace AssetStudioCLI
{
    class Program
    {
        public static void Main(string[] args)
        {
            CLIOptions.ParseArgs(args);
            if (CLIOptions.isParsed)
            {
                CLIRun();
            }
            else if (CLIOptions.f_displayHelp.Value)
            {
                CLIOptions.ShowHelp();
            }
            else
            {
                Console.WriteLine();
                CLIOptions.ShowHelp(showUsageOnly: true);
            }
        }

        private static void CLIRun()
        {
            AssetStudioCliRunner.RunParsed(catchExceptions: true);
        }       
    }
}
