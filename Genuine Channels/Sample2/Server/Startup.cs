using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Owin;
using Owin;
using Server.GC_Code;
using KnownObjects;

[assembly: OwinStartup(typeof(Server.Startup))]

namespace Server
{
    public partial class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            ConfigureAuth(app);

            GC_Server.StartServer(enumGC_Mode.GC_HTTP); //GC

        }
    }
}
