using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace DataVoyager.Export.Internals;

internal class Parameters
{
    public required string OutputFilename { get; set; }

    public bool All { get; set; }

    public bool ForeignKeys { get; set; }

    public bool Functions { get; set; }

    public bool Indexes { get; set; }

    public bool Procedures { get; set; }

    public bool Tables { get; set; }

    public bool Triggers { get; set; }

    public bool Schemas { get; set; }

    public bool Users { get; set; }

    public bool Views { get; set; }
    public bool DbObjects
    {
        get => ForeignKeys || Functions || Indexes || Procedures || Tables || Triggers || Schemas || Users || Views;
        set => ForeignKeys = Functions = Indexes = Procedures = Tables = Triggers = Schemas = Users = Views = value;
    }
}
