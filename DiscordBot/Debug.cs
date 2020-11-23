using Discord;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot
{
    public static class Debug
    {
        public static Task Log(LogMessage message)
        {
            ConsoleColor cc = Console.ForegroundColor;
            switch (message.Severity)
            {
                case LogSeverity.Critical:
                case LogSeverity.Error:
                    Console.ForegroundColor = ConsoleColor.Red;
                    break;
                case LogSeverity.Warning:
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    break;
                case LogSeverity.Info:
                    Console.ForegroundColor = ConsoleColor.White;
                    break;
                case LogSeverity.Verbose:
                case LogSeverity.Debug:
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    break;
            }

            Console.WriteLine($"{DateTime.Now,-19} [{message.Severity,8}] {message.Source}: {message.Message}");

            Console.ForegroundColor = cc;
            return Task.CompletedTask;
        }

        public static Task Log(string src, object message)
        {
            ConsoleColor cc = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.DarkGray;

            Console.WriteLine($"{DateTime.Now,-19} [{"Debug",8}] {src}: {message.ToString()}");

            Console.ForegroundColor = cc;
            return Task.CompletedTask;
        }

        public static Task LogError(string src, object message)
        {
            ConsoleColor cc = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Red;

            Console.WriteLine($"{DateTime.Now,-19} [{"Debug",8}] {src}: {message.ToString()}");

            Console.ForegroundColor = cc;
            return Task.CompletedTask;
        }
    }
}
