# EasyMenu Web

Panel web para el personal de un restaurante (ADMINISTRADOR, CAJERO y COCINA).
Es ASP.NET Core MVC (.NET 10) con Bootstrap. No hay parte para clientes.
La web llama a una API REST (Spring Boot) desde el servidor con HttpClient; el navegador nunca llama a la API directamente.

## Reglas
- Interfaz y mensajes en español. Comentarios del código en español y simples.
- URL de la API en appsettings.json (clave ApiBaseUrl), nunca escrita en el código.
  Valor: https://easymenu-api-1nce.onrender.com/api
- El servidor de la API es gratuito y puede tardar hasta 50 segundos en responder la primera vez: usa un timeout de 90 segundos y muestra un mensaje de "cargando".
- El token JWT y el rol se guardan en la sesión del servidor, no en el navegador.
- No instales paquetes nuevos ni cambies archivos que no te pida; si hace falta algo, pregúntame.
- Haz cambios pequeños, un tema a la vez.

## API (ya probada)
- POST /auth/login  cuerpo {correo, clave}  respuesta {token, tipo, rol}. Roles: ADMINISTRADOR, CAJERO, COCINA.
- Rutas del personal llevan el encabezado Authorization: Bearer {token}.
- GET /pedidos/{id}, GET /pedidos/codigo/{codigoCorto}, GET /pedidos/estado/{estado}  (ADMINISTRADOR, CAJERO, COCINA)
- PATCH /pedidos/{id}/estado?estado=X  (el estado va en la URL, no en el cuerpo)
- Flujo de estados: PENDIENTE_PAGO -> (cobro) -> RECIBIDO -> EN_PREPARACION -> LISTO -> ENTREGADO. No se puede saltar pasos.
- POST /pagos/cobrar  cuerpo {pedidoId, metodoPago, montoRecibido}. metodoPago: EFECTIVO, TARJETA o TRANSFERENCIA. montoRecibido solo es obligatorio en EFECTIVO. Solo CAJERO y ADMINISTRADOR. Devuelve el cambio.
- PATCH /pagos/{id}/reembolsar?motivo=texto  (CAJERO y ADMINISTRADOR)
- GET /productos/disponibles (público). Producto: id, nombre, descripcion, precio, categoria, disponible, activo.
- POST/PUT /productos y PATCH /productos/{id}/disponibilidad: solo ADMINISTRADOR. Cuerpo de POST/PUT: {nombre, descripcion, precio, categoria}.
- Errores de negocio: JSON {estado, mensaje, fecha} con 400 o 404; muestra el campo mensaje al usuario.
- 401/403: sin permiso o sesión vencida; llevar al login con aviso.