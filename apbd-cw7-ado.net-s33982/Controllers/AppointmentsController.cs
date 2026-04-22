using apbd_cw7_ado.net_s33982.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace apbd_cw7_ado.net_s33982.Controllers {
    [Route("api/[controller]")]
    [ApiController]
    public class AppointmentsController : ControllerBase {
        private readonly string connectionString;

        public AppointmentsController(IConfiguration configuration) {
            connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        [HttpGet]
        public async Task<IActionResult> Get([FromQuery] AppointmentSearchDTO search) {
            var lista = new List<AppointmentListDTO>();
            try {
                await using (var connection = new SqlConnection(connectionString)) {
                    await connection.OpenAsync();

                    await using var command = new SqlCommand("""
                                                             SELECT
                                                                 a.IdAppointment,
                                                                 a.AppointmentDate,
                                                                 a.Status,
                                                                 a.Reason,
                                                                 p.FirstName + N' ' + p.LastName AS PatientFullName,
                                                                 p.Email AS PatientEmail
                                                             FROM Appointments a
                                                             JOIN Patients p ON p.IdPatient = a.IdPatient
                                                             WHERE (@Status IS NULL OR a.Status = @Status)
                                                               AND (@PatientLastName IS NULL OR p.LastName = @PatientLastName)
                                                             ORDER BY a.AppointmentDate;
                                                             """, connection);
                    command.Parameters.AddWithValue("@Status", search.status.Length > 0 ? search.status : DBNull.Value);
                    command.Parameters.AddWithValue("@PatientLastName", search.patientLastName.Length > 0 ? search.patientLastName : DBNull.Value);

                    await using var reader = await command.ExecuteReaderAsync();

                    while (await reader.ReadAsync()) {
                        var id = reader.GetInt32(0);
                        var appDate = reader.GetDateTime(1);
                        var status = reader.GetString(2);
                        var reason = reader.GetString(3);
                        var patientFullName = reader.GetString(4);
                        var patientEmail = reader.GetString(5);
                        var temp = new AppointmentListDTO(id, appDate, status, reason, patientFullName, patientEmail);
                        lista.Add(temp);
                    }

                    if (lista.Count == 0) {
                        return NotFound();
                    }

                    return Ok(lista);
                }
            }
            catch (Exception e) {
                return StatusCode(500, $"Błąd bazy danych: {e.Message}");
            }
        }

        [HttpGet]
        [Route("{id}")]
        public async Task<IActionResult> GetById([FromRoute] int id) {
            try {
                await using (var connection = new SqlConnection(connectionString)) {
                    await connection.OpenAsync();

                    await using var command = new SqlCommand("""
                                                             SELECT
                                                                 IdAppointment,
                                                                 IdPatient,
                                                                 IdDoctor,
                                                                 AppointmentDate,
                                                                 Status,
                                                                 Reason,
                                                                 InternalNotes
                                                             FROM Appointments WHERE IdAppointment = @IdAppointment
                                                             """, connection);
                    command.Parameters.AddWithValue("@IdAppointment", id);

                    await using var reader = await command.ExecuteReaderAsync();

                    while (await reader.ReadAsync()) {
                        var idAppointment = reader.GetInt32(0);
                        var idPatient = reader.GetInt32(1);
                        var idDoctor = reader.GetInt32(2);
                        var appointmentDate = reader.GetDateTime(3);
                        var status = reader.GetString(4);
                        var reason = reader.GetString(5);
                        var internalNotes = reader.IsDBNull(6) ? null : reader.GetString(6);
                        return Ok(new AppointmentDetailsDTO(idAppointment, idPatient, idDoctor, appointmentDate, status, reason, internalNotes));
                    }

                    return NotFound();
                }
            }
            catch (Exception e) {
                return StatusCode(500, $"Błąd bazy danych: {e.Message}");
            }
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] CreateAppointmentRequestDTO createAppointmentRequestDto) {
            try {
                await using (var connection = new SqlConnection(connectionString)) {
                    await connection.OpenAsync();

                    {
                        await using var patientExists = new SqlCommand("""
                                                                       SELECT
                                                                           IdPatient,
                                                                           IsActive
                                                                       FROM Patients WHERE IdPatient = @IdPatient AND IsActive = 1
                                                                       """, connection);
                        patientExists.Parameters.AddWithValue("@IdPatient", createAppointmentRequestDto.idPatient);

                        await using var readerPatient = await patientExists.ExecuteReaderAsync();
                        if (!await readerPatient.ReadAsync()) {
                            return Conflict("Pacjent nie istnieje");
                        }
                    }

                    {
                        await using var doctorExists = new SqlCommand("""
                                                                      SELECT
                                                                          IdDoctor,
                                                                          IsActive
                                                                      FROM Doctors WHERE IdDoctor = @IdDoctor AND IsActive = 1
                                                                      """, connection);
                        doctorExists.Parameters.AddWithValue("@IdDoctor", createAppointmentRequestDto.idDoctor);

                        await using var readerDoctor = await doctorExists.ExecuteReaderAsync();
                        if (!await readerDoctor.ReadAsync()) {
                            return Conflict("Doktor nie istnieje");
                        }

                        if (createAppointmentRequestDto.appointmentDate < DateTime.Now) {
                            return Conflict("Data w przeszłości");
                        }

                        if (createAppointmentRequestDto.reason == null || createAppointmentRequestDto.reason.Length == 0 || createAppointmentRequestDto.reason.Length > 250) {
                            return Conflict("Nie podano reason lub nie miesci sie w przedziale (0, 250]");
                        }
                    }

                    {
                        await using var hasAppointment = new SqlCommand("""
                                                                        SELECT
                                                                            AppointmentDate
                                                                        FROM Appointments WHERE IdDoctor = @IdDoctor AND AppointmentDate = @AppoinmentDate
                                                                        """, connection);
                        hasAppointment.Parameters.AddWithValue("@IdDoctor", createAppointmentRequestDto.idDoctor);
                        hasAppointment.Parameters.AddWithValue("@AppoinmentDate", createAppointmentRequestDto.appointmentDate);

                        await using var hasAppointmentreader = await hasAppointment.ExecuteReaderAsync();
                        if (await hasAppointmentreader.ReadAsync()) {
                            return Conflict("Doktor ma wtedy wizyte");
                        }
                    }

                    await using var insertCommand = new SqlCommand("INSERT INTO Appointments (IdPatient, IdDoctor, AppointmentDate, Status, Reason, InternalNotes) VALUES (@IdPatient, @IdDoctor, @AppointmentDate, 'Scheduled', @Reason, '')",
                        connection);
                    insertCommand.Parameters.AddWithValue("@IdPatient", createAppointmentRequestDto.idPatient);
                    insertCommand.Parameters.AddWithValue("@IdDoctor", createAppointmentRequestDto.idDoctor);
                    insertCommand.Parameters.AddWithValue("@AppointmentDate", createAppointmentRequestDto.appointmentDate);
                    insertCommand.Parameters.AddWithValue("@Reason", createAppointmentRequestDto.reason);

                    var rowsInserted = (int)await insertCommand.ExecuteNonQueryAsync();
                    Console.WriteLine(rowsInserted);
                    return Created();
                }
            }
            catch (Exception e) {
                return StatusCode(500, $"Błąd bazy danych: {e.Message}");
            }
        }

        [HttpPut]
        [Route("{id}")]
        public async Task<IActionResult> Put([FromRoute] int id, [FromBody] UpdateAppointmentRequestDTO updateAppointmentRequestDto) {
            try {
                await using (var connection = new SqlConnection(connectionString)) {
                    await connection.OpenAsync();
                    {
                        await using var appDontExist = new SqlCommand("""
                                                                      SELECT
                                                                          IdAppointment
                                                                      FROM Appointments WHERE IdAppointment = @IdAppointment
                                                                      """, connection);
                        appDontExist.Parameters.AddWithValue("@IdAppointment", id);

                        await using var render = await appDontExist.ExecuteReaderAsync();
                        if (!await render.ReadAsync()) {
                            return NotFound();
                        }
                    }

                    {
                        await using var patientExists = new SqlCommand("""
                                                                       SELECT
                                                                           IdPatient,
                                                                           IsActive
                                                                       FROM Patients WHERE IdPatient = @IdPatient AND IsActive = 1
                                                                       """, connection);
                        patientExists.Parameters.AddWithValue("@IdPatient", updateAppointmentRequestDto.idPatient);

                        await using var readerPatient = await patientExists.ExecuteReaderAsync();
                        if (!await readerPatient.ReadAsync()) {
                            return Conflict("Pacjent nie istnieje");
                        }
                    }

                    {
                        await using var doctorExists = new SqlCommand("""
                                                                      SELECT
                                                                          IdDoctor,
                                                                          IsActive
                                                                      FROM Doctors WHERE IdDoctor = @IdDoctor AND IsActive = 1
                                                                      """, connection);
                        doctorExists.Parameters.AddWithValue("@IdDoctor", updateAppointmentRequestDto.idDoctor);

                        await using var readerDoctor = await doctorExists.ExecuteReaderAsync();
                        if (!await readerDoctor.ReadAsync()) {
                            return Conflict("Doktor nie istnieje");
                        }
                    }

                    if (!updateAppointmentRequestDto.status.Equals("Scheduled") && !updateAppointmentRequestDto.status.Equals("Completed") && !updateAppointmentRequestDto.status.Equals("Cancelled")) {
                        return Conflict("zly status");
                    }

                    {
                        await using var getStatus = new SqlCommand("""
                                                                   SELECT
                                                                       Status,
                                                                       AppointmentDate
                                                                   FROM Appointments WHERE IdAppointment = @IdAppointment
                                                                   """, connection);
                        getStatus.Parameters.AddWithValue("@IdAppointment", id);

                        await using var reader = await getStatus.ExecuteReaderAsync();
                        if (await reader.ReadAsync()) {
                            var status = reader.GetString(0);
                            var appointmentDate = reader.GetDateTime(1);
                            if (status.Equals("Completed") && appointmentDate != updateAppointmentRequestDto.appointmentDate) {
                                return Conflict("Nie mozna zmienic daty");
                            }
                        }
                    }

                    {
                        await using var appConflict = new SqlCommand("""
                                                                     SELECT
                                                                         AppointmentDate
                                                                     FROM Appointments WHERE IdDoctor = @IdDoctor AND AppointmentDate = @AppoinmentDate
                                                                     """, connection);
                        appConflict.Parameters.AddWithValue("@IdDoctor", updateAppointmentRequestDto.idDoctor);
                        appConflict.Parameters.AddWithValue("@AppoinmentDate", updateAppointmentRequestDto.appointmentDate);

                        await using var reader = await appConflict.ExecuteReaderAsync();
                        if (await reader.ReadAsync()) {
                            return Conflict("Doktor ma wtedy wizyte");
                        }
                    }

                    {
                        await using var update = new SqlCommand(
                            "UPDATE Appointments SET IdPatient = @IdPatient, IdDoctor = @IdDoctor, AppointmentDate = @AppointmentDate, Status = @Status, Reason = @Reason, InternalNotes = @InternalNotes WHERE IdAppointment = @IdAppointment",
                            connection);
                        update.Parameters.AddWithValue("@IdPatient", updateAppointmentRequestDto.idPatient);
                        update.Parameters.AddWithValue("@IdDoctor", updateAppointmentRequestDto.idDoctor);
                        update.Parameters.AddWithValue("@AppointmentDate", updateAppointmentRequestDto.appointmentDate);
                        update.Parameters.AddWithValue("@Status", updateAppointmentRequestDto.status);
                        update.Parameters.AddWithValue("@Reason", updateAppointmentRequestDto.reason);
                        update.Parameters.AddWithValue("@InternalNotes", updateAppointmentRequestDto.internalNotes);
                        update.Parameters.AddWithValue("@IdAppointment", id);

                        int rows = await update.ExecuteNonQueryAsync();

                        return Ok();
                    }
                }
            }
            catch (Exception e) {
                return StatusCode(500, $"Błąd bazy danych: {e.Message}");
            }
        }

        [HttpDelete]
        [Route("{id}")]
        public async Task<IActionResult> Delete(int id) {
            try {
                await using (var connection = new SqlConnection(connectionString)) {
                    await connection.OpenAsync();

                    {
                        await using var getData = new SqlCommand("""
                                                                 SELECT
                                                                     IdAppointment,
                                                                     Status
                                                                 FROM Appointments WHERE IdAppointment = @IdAppointment
                                                                 """, connection);
                        getData.Parameters.AddWithValue("@IdAppointment", id);

                        await using var reader = await getData.ExecuteReaderAsync();

                        if (await reader.ReadAsync()) {
                            var status =  reader.GetString(1);
                            if (status.Equals("Completed")) {
                                return Conflict();
                            }
                        }
                        else {
                            return NotFound();
                        }
                    }
                    
                    {
                        await using var delete = new SqlCommand("DELETE FROM Appointments WHERE IdAppointment = @IdAppointment", connection);
                        delete.Parameters.AddWithValue("@IdAppointment", id);

                        var reader = await delete.ExecuteNonQueryAsync();
                        Console.WriteLine(reader);
                        return NoContent();
                    }
                }
            }
            catch (Exception e) {
                return StatusCode(500, $"Błąd bazy danych: {e.Message}");
            }
        }
    }
}