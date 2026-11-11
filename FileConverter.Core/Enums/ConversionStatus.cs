using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using FileConverter.Core.Enums;



namespace FileConverter.Core.Enums
{
    public enum ConversionStatus
    {
        Pending,
        Validating,
        Waiting,
        Converting,
        Completed,
        Failed,
        Cancelled
    }
}
