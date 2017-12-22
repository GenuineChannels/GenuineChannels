using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KnownObjects
{
    /// <summary>
    /// For ease of use set which mode GC works in
    /// Both Client and Server need to be the same mode.
    /// </summary>
    public enum enumGC_Mode
    {
        GC_TCP,
        GC_HTTP
    }
}
