using System;
using System.Linq;
using System.Threading.Tasks;

using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordBot
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class RequireAdminAttribute : PreconditionAttribute
    {
        public override Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
        {
            SocketGuildUser user = context.Message.Author as SocketGuildUser;

            if (user.Roles.Any(x => x.Permissions.Administrator))
            {
                return Task.FromResult(PreconditionResult.FromSuccess());
            }
            return Task.FromResult(PreconditionResult.FromError(user + " attempted to use admin only command!"));
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class RequireStaffAttribute : PreconditionAttribute
    {
        public override Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
        {
            SocketGuildUser user = context.Message.Author as SocketGuildUser;
            Settings.Deserialized.Settings settings = services.GetRequiredService<Settings.Deserialized.Settings>();
            
            if (user.Roles.Any(x => x.Id == settings.StaffRoleId))
            {
                return Task.FromResult(PreconditionResult.FromSuccess());
            }
            return Task.FromResult(PreconditionResult.FromError(user + " attempted to use a staff only command!"));
        }
    }
}
