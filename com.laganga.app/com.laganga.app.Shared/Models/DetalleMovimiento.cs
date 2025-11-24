using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace com.laganga.app.Shared.Models;

public interface IDetalleMovimiento
{
    string CodigoItem { get; set; }
    string Descripcion { get; set; }
    int Cantidad { get; set; }
    int SeriesPorUnidad { get; set; }
    string CodigoFabrica { get; set; }
    List<string> SeriesCapturadas { get; set; }
}

public class DetalleMovimiento : IDetalleMovimiento
{
    public string CodigoItem { get; set; }
    public string Descripcion { get; set; }
    public int Cantidad { get; set; }
    public int SeriesPorUnidad { get; set; }
    public string CodigoFabrica { get; set; }
    public List<string> SeriesCapturadas { get; set; }
    public int CantidadIngresada => SeriesCapturadas.Count() > 0 ? SeriesCapturadas.Count() / SeriesPorUnidad : 0;
    public int TotalSeriesEsperadas => Cantidad * SeriesPorUnidad;
    public int TotalSeriesIngresadas => SeriesCapturadas.Count();
    
}


