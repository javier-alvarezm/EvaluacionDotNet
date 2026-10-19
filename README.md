# Evaluación .NET

API RESTful en .NET 8 con Controllers, Entity Framework Core y SQL Server.

## Iniciar

Desde la raíz del repositorio:

```powershell
dotnet run --project src/EvaluacionDotNet.Api --launch-profile https
```
/
Si hay una instancia anterior ejecutándose, detenla con Ctrl+C y vuelve a iniciar. La dirección es https://localhost:7131. GET /health comprueba la conexión SQL.

## Requerimientos del Proyecto

Tabla de Monedas (Currency):

- Id (int, Primary Key)
- Symbol (string)
- Name (string)

Tabla de Tasas de Cambio (ExchangeRates):

- Id (int, Primary Key)
- BaseCurrency (string, Foreign Key to Currency)
- TargetCurrency (string, Foreign Key to Currency)
- Rate (decimal)
- Date (DateTime)

Las relaciones utilizan Currency.Symbol. Las tasas se guardan con precisión decimal(28,12) y la fecha como datetime2. Se trabaja por día, normalizando Date a medianoche. El par base/destino y fecha es único. La migración incluye USD y EUR para empezar; las importaciones crean otras monedas cuando hace falta. Las únicas tablas de negocio son Currency y ExchangeRates; Entity Framework mantiene además su historial de migraciones.

## Endpoints CRUD

Implementar los siguientes endpoints para gestionar las tasas de cambio almacenadas:

- GET /rates: Devuelve la lista de tasas de cambio almacenadas.
- GET /rates/{id}: Devuelve una tasa de cambio específica por Id.
- POST /rates: Crea nuevas tasas de cambio.
- PUT /rates/{id}: Actualiza una tasa de cambio existente por Id.
- DELETE /rates/{id}: Elimina una tasa de cambio por Id.
- GET /rates/currency/{baseCurrency}: Devuelve las tasas de cambio por moneda base.
- PUT /rates/currency/{baseCurrency}: Actualiza las tasas de cambio por moneda base (requiere pasar un body con los datos a actualizar).
- DELETE /rates/currency/{baseCurrency}: Elimina las tasas de cambio por moneda base.

POST /rates y PUT /rates/{id} reciben:

```json
{
  "baseCurrency": "USD",
  "targetCurrency": "EUR",
  "rate": 0.92,
  "date": "2024-01-02T00:00:00"
}
```

PUT /rates/currency/{baseCurrency} recibe el valor que se aplicará a todas las tasas de esa moneda base:

```json
{ "rate": 0.92 }
```

## Transformación de Datos

Implementar endpoints que realicen cálculos sobre los datos almacenados:

- GET /rates/average?base={baseCurrency}&target={targetCurrency}&start={startDate}&end={endDate}: Devuelve el valor promedio de la tasa de cambio entre las fechas especificadas.
- GET /rates/minmax?base={baseCurrency}&target={targetCurrency}&start={startDate}&end={endDate}: Devuelve los valores mínimo y máximo de la tasa de cambio entre las fechas especificadas.

Las fechas de consulta usan yyyy-MM-dd. El intervalo incluye ambas fechas. Sin observaciones se devuelve 404.

## JWT

Envía POST /auth/token sin body ni credenciales. Devuelve accessToken, tokenType, expiresIn (7200 segundos) y expiresAtUtc. El token dura dos horas. Este endpoint público entrega el token solicitado para el ejercicio; no identifica usuarios. No hay registro, login ni tabla de usuarios.

Para llamar a los endpoints protegidos:

```http
Authorization: Bearer <accessToken>
```

La clave de firma se guarda fuera del código, en User Secrets. POST /auth/token y GET /health son públicos. Los endpoints de tasas y Frankfurter requieren el token.

## Consulta a la API de Frankfurter

Se conserva esta parte del enunciado original:

- GET /frankfurter/latest?base=USD&target=EUR
- GET /frankfurter/historical?base=USD&target=EUR&date=2024-01-02
- GET /frankfurter/timeseries?base=USD&target=EUR&start=2024-01-02&end=2024-01-05
- GET /frankfurter/convert?from=USD&to=EUR&amount=100&date=2024-01-02
- POST /frankfurter/import/latest
- POST /frankfurter/import/historical
- POST /frankfurter/import/timeseries

Las consultas GET devuelven datos; las importaciones POST los guardan en SQL Server. Para importar recientes usa baseCurrency y targetCurrency. Para importar históricas agrega date; para series, start y end. Los ejemplos están en Postman. Reimportar un par y fecha actualiza su tasa.

## Postman

Importa postman/EvaluacionDotNet.postman_collection.json y ejecuta las carpetas en orden. La primera petición obtiene y guarda automáticamente el JWT. Los nombres y descripciones de los endpoints de tasas reproducen el enunciado.

La colección prueba el CRUD con EUR/USD y fechas de enero de 2020. Incluye DELETE /rates/currency/EUR, que elimina todas las tasas de esa base. Ejecútala sobre una base de pruebas. Las importaciones usan USD/EUR.

## Preparar y verificar

Base local: EvaluacionDotNet_Dev, servidor .\\SQLEXPRESS, autenticación de Windows. La cadena de conexión está en appsettings.json. Requiere .NET SDK 8 y SQL Server Express.

```powershell
powershell -ExecutionPolicy Bypass -File scripts/Prepare-Local.ps1
powershell -ExecutionPolicy Bypass -File scripts/Test-Integration.ps1 -Postman
```

El primer script restaura, configura la clave JWT y aplica las migraciones. El segundo crea una base SQL temporal aislada, prueba la API y Postman y elimina solo su base de pruebas. Newman requiere Node.js y acceso a npm. Usa -SkipExternal sin -Postman para omitir las consultas externas.

La migración SimplifyAssignment elimina la tabla Users de la versión anterior. En este equipo se guardó una copia de sus datos anteriores en .artifacts/legacy-users-backup.json, excluida del repositorio. Las monedas y tasas existentes se conservan.

