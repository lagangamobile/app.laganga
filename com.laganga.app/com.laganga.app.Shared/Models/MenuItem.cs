using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace com.laganga.app.Shared.Models;

public interface IMenuItem
{
    int Id { get; set; }
    string Nombre { get; set; }
    string? Icono { get; set; }
    string? Ruta { get; set; }
    bool Visible { get; set; }
    int Orden { get; set; }
    List<MenuItem> Hijos { get; set; }
}

public class MenuItem : IMenuItem
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Icono { get; set; }
    public string? Ruta { get; set; }
    public bool Visible { get; set; }
    public int Orden { get; set; }
    public List<MenuItem> Hijos { get; set; } = new();
}