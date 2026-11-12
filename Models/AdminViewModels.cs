using System.ComponentModel.DataAnnotations;

namespace EasyMenu.Web.Models;

public sealed class ProductosAdminViewModel
{
    public List<ProductoAdminViewModel> Productos { get; init; } = [];
    public string? Mensaje { get; init; }
}

public sealed class PedidosAdminViewModel
{
    public List<PedidoAdminViewModel> Pedidos { get; init; } = [];
    public string? Estado { get; init; }
    public string? Mensaje { get; init; }
}

public sealed class PedidoAdminViewModel
{
    public string Id { get; init; } = string.Empty;
    public string CodigoCorto { get; init; } = string.Empty;
    public string ClienteOMesa { get; init; } = string.Empty;
    public string Estado { get; init; } = string.Empty;
    public decimal Total { get; init; }
    public DateTime? FechaHora { get; init; }
    public List<ProductoPedidoAdminViewModel> Productos { get; init; } = [];
    public string? Mensaje { get; init; }
}

public sealed class ProductoPedidoAdminViewModel
{
    public string Nombre { get; init; } = string.Empty;
    public string Cantidad { get; init; } = string.Empty;
    public decimal PrecioUnitario { get; init; }
    public decimal Subtotal { get; init; }
    public string? Notas { get; init; }
}

public sealed class ProductoAdminViewModel
{
    public string Id { get; init; } = string.Empty;
    public string Nombre { get; init; } = string.Empty;
    public string Descripcion { get; init; } = string.Empty;
    public string Categoria { get; init; } = string.Empty;
    public decimal Precio { get; init; }
    public bool Disponible { get; init; }
}

public sealed class ProductoFormularioViewModel
{
    public string? Id { get; set; }
    public string? CategoriaOriginal { get; set; }

    [Required(ErrorMessage = "Ingresa el nombre del producto.")]
    [StringLength(100, ErrorMessage = "El nombre no puede superar los 100 caracteres.")]
    public string Nombre { get; set; } = string.Empty;

    [StringLength(255, ErrorMessage = "La descripción no puede superar los 255 caracteres.")]
    public string? Descripcion { get; set; }

    [Range(typeof(decimal), "0.0000000000000000000000000001", "79228162514264337593543950335",
        ErrorMessage = "El precio debe ser mayor que 0.")]
    public decimal Precio { get; set; }

    [Required(ErrorMessage = "Selecciona una categoría.")]
    public string Categoria { get; set; } = string.Empty;
}
