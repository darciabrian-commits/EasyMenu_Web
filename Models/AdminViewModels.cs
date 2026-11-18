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

    [Required(ErrorMessage = "Ingresa el nombre del producto.")]
    [StringLength(100, ErrorMessage = "El nombre no puede superar los 100 caracteres.")]
    public string Nombre { get; set; } = string.Empty;

    [StringLength(255, ErrorMessage = "La descripción no puede superar los 255 caracteres.")]
    public string? Descripcion { get; set; }

    [Range(typeof(decimal), "0.0000000000000000000000000001", "79228162514264337593543950335",
        ErrorMessage = "El precio debe ser mayor que 0.")]
    public decimal Precio { get; set; }

    [Required(ErrorMessage = "Selecciona una categoría.")]
    [RegularExpression("^(PLATILLO|BEBIDA|POSTRE)$", ErrorMessage = "Selecciona una categoría válida.")]
    public string Categoria { get; set; } = string.Empty;
}

public sealed class ResumenVentasViewModel
{
    public string Fecha { get; init; } = string.Empty;
    public ResumenVentasResponseDTO? Resumen { get; init; }
    public string? Mensaje { get; init; }
}

public sealed class ResumenVentasResponseDTO
{
    public required DateOnly Fecha { get; init; }
    public required int CantidadCobrosRegistrados { get; init; }
    public required decimal TotalCobradoEfectivo { get; init; }
    public required decimal TotalCobradoTarjeta { get; init; }
    public required decimal TotalCobradoTransferencia { get; init; }
    public required decimal TotalBrutoVentas { get; init; }
    public required int CantidadReembolsosRegistrados { get; init; }
    public required decimal TotalReembolsos { get; init; }
    public required decimal TotalNetoVentas { get; init; }
}

public sealed class PersonalAdminViewModel
{
    public List<UsuarioPersonalViewModel> Usuarios { get; init; } = [];
    public string? Mensaje { get; init; }
}

public sealed class UsuarioPersonalViewModel
{
    public string Id { get; init; } = string.Empty;
    public string Nombre { get; init; } = string.Empty;
    public string Correo { get; init; } = string.Empty;
    public string Rol { get; init; } = string.Empty;
    public bool Activo { get; init; }
    public string? Mensaje { get; init; }
}

public sealed class UsuarioFormularioViewModel
{
    [Required(ErrorMessage = "Ingresa el nombre.")]
    public string Nombre { get; set; } = string.Empty;

    [Required(ErrorMessage = "Ingresa el correo.")]
    [EmailAddress(ErrorMessage = "Ingresa un correo válido.")]
    public string Correo { get; set; } = string.Empty;

    [Required(ErrorMessage = "Ingresa la contraseña.")]
    [DataType(DataType.Password)]
    public string Clave { get; set; } = string.Empty;

    [Required(ErrorMessage = "Selecciona un rol.")]
    [RegularExpression("^(CAJERO|COCINA)$", ErrorMessage = "Solo puedes crear usuarios CAJERO o COCINA.")]
    public string Rol { get; set; } = string.Empty;
}
