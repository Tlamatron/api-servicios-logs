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

## 📌 Endpoints principales

### 1. Crear Token
`POST /api/token/crearToken`

**Body (JSON):**
```json
{
  "User": "usuario1",
  "Password": "pwd123",
  "Ambiente": 0,
  "nombAplicacion": "postman",
  "Motor": 0,
  "TiempoActivo": 60
}