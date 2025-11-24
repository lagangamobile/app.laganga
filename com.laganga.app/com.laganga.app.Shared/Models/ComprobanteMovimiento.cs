using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace com.laganga.app.Shared.Models;

public interface IComprobanteMovimiento
{
    string SeqCompte { get; set; }
    string NumCompte { get; set; }
    string Bodega { get; set; }
    string BodegaNombre { get; set; }
    DateTime Fecha { get; set; }
    string Cliente { get; set; }
    string DestinoProvinciaId { get; set; }
    string DestinoCantonId { get; set; }
    string DestinoCiudadId { get; set; }
    string DestinoCiudad { get; set; }
    string Destinatario { get; set; }
    string PuntoLlegada { get; set; }
    string Comentario { get; set; }
    List<DetalleMovimiento> Detalles { get; set; }
}

public class ComprobanteMovimiento : IComprobanteMovimiento
{
    public string SeqCompte { get; set; }
    public string NumCompte { get; set; }
    public string Bodega { get; set; }
    public string BodegaNombre { get; set; }
    public DateTime Fecha { get; set; }
    public string Cliente { get; set; }
    public string DestinoProvinciaId { get; set; }
    public string DestinoCantonId { get; set; }
    public string DestinoCiudadId { get; set; }
    public string DestinoCiudad { get; set; }
    public string Destinatario { get; set; }
    public string PuntoLlegada { get; set; }
    public string Comentario { get; set; }
    public List<DetalleMovimiento> Detalles { get; set; }
}

