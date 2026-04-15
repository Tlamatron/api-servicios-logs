# API Servicios Logs (swLog)

API institucional para gestión de logs y auditoría, desarrollada en ASP.NET Web API con NLog.

## 🚀 Características principales
- CORS dinámico según entorno (`Cors.config`)
- Rate limiting por cliente
- Validación de `Content-Type` en POST/PUT
- Manejo global de excepciones
- Documentación XML integrada
- Logging por aplicación (`Log_{nombreAplicacion}.log`)
- Auditoría central (`auditoria.log`)

---

## ⚙️ Configuración

Este proyecto requiere dos archivos de configuración en la carpeta `Config`:

- **secrets.config** → Contiene credenciales y llaves sensibles. **No se versiona**.  
- **Cors.config** → Contiene orígenes y aplicaciones permitidas. **No se versiona**.  

Ejemplos disponibles en el repositorio:
- `secrets.config.example`
- `Cors.config.example`

Al clonar el proyecto:
1. Copiar los `.example` y renombrarlos a `.config`.
2. Reemplazar los valores ficticios por los reales de tu entorno.

---

## 📌 Endpoints principales

### LogController (`api/log`)

#### 1. Escribir log
`POST /api/log/write/{nombreAplicacion}`

**Body (JSON):**
```json
"Mensaje de prueba desde Postman"

2. Listar logs
GET /api/log/list
Response (JSON):
["Log_swImagenInstitucional.log", "Log_swFinanzas.log"]

3. Asignar responsable
POST /api/log/setResponsable
Body (JSON):
{
  "Responsable": "Juan Pérez",
  "Aplicacion": "swImagenInstitucional"
}

4. Consultar responsables
GET /api/log/responsables
Response (JSON):
["Juan Pérez", "María López"]

WsLogsController (api/wsLogs)
1. Crear Token
POST /api/wsLogs/crearToken
Body (JSON):
{
  "User": "usuario1",
  "Password": "pwd123",
  "Ambiente": 0,
  "nombAplicacion": "postman",
  "Motor": 0,
  "TiempoActivo": 60
}

2. Validar Token
POST /api/wsLogs/validarToken
Body (JSON):
{
  "Token": "abc123xyz"
}

3. Generar JWT
POST /api/wsLogs/generateJwt
Body (JSON):
{
  "User": "usuario1",
  "Aplicacion": "swImagenInstitucional"
}

4. Validar JWT
POST /api/wsLogs/validateJwt
Body (JSON):
{
  "Jwt": "eyJhbGciOi..."
}

5. Extender JWT
POST /api/wsLogs/extendJwt
Body (JSON):
{
  "Jwt": "eyJhbGciOi...",
  "MinutosExtra": 30
}