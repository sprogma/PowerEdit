using System;
using System.Collections.Generic;
using System.Text;

namespace Common
{
    public class Config
    {
        static public string Directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PowerEdit");
    }
}
