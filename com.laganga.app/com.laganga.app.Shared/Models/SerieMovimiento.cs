using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace com.laganga.app.Shared.Models;

public interface ISerieMovimiento
{
    string SeqCompte { get; set; }
    string CodigoItem { get; set; }
    string Serie { get; set; }
}

public class SerieMovimiento : ISerieMovimiento
{
    public string SeqCompte { get; set; } = string.Empty;
    public string CodigoItem { get; set; } = string.Empty;
    public string Serie { get; set; } = string.Empty;
}