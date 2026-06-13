// ========================
//  Global state
// ========================
let schedules = [];
let selectedFrequency = null;

// ========================
//  Frequency selection handler
// ========================
function selectFrequency(freq) {
    selectedFrequency = freq;

    // Update card selection
    document.querySelectorAll(".frequency-card").forEach(card => {
        card.classList.toggle("selected", card.dataset.frequency === freq);
    });

    // Show/hide conditional fields
    document.querySelectorAll(".frequency-fields").forEach(f => f.classList.remove("visible"));
    const target = document.getElementById(`${freq}Fields`);
    if (target) target.classList.add("visible");
}

// ========================
//  Cron expression generator
// ========================
function generateCronExpression() {
    if (!selectedFrequency) return null;

    switch (selectedFrequency) {
        case "hourly": {
            const minute = document.getElementById("hourlyMinute").value || "0";
            return `${minute} * * * *`;
        }
        case "daily": {
            const time = document.getElementById("dailyTime").value || "08:00";
            const [h, m] = time.split(":");
            return `${m} ${h} * * *`;
        }
        case "weekly": {
            const time = document.getElementById("weeklyTime").value || "08:00";
            const day = document.getElementById("weeklyDay").value || "1";
            const [h, m] = time.split(":");
            return `${m} ${h} * * ${day}`;
        }
        case "monthly": {
            const time = document.getElementById("monthlyTime").value || "08:00";
            const day = document.getElementById("monthlyDay").value || "1";
            const [h, m] = time.split(":");
            return `${m} ${h} ${day} * *`;
        }
        default:
            return null;
    }
}

// ========================
//  Cron expression parser (for edit mode)
// ========================
function parseCronExpression(cron) {
    if (!cron) return { frequency: null, values: {} };

    const parts = cron.trim().split(/\s+/);
    if (parts.length < 5) return { frequency: null, values: {} };

    const [minute, hour, dom, month, dow] = parts;

    // Cada Hora: minute * * * *
    if (hour === "*" && dom === "*" && month === "*" && dow === "*") {
        return { frequency: "hourly", values: { minute } };
    }

    // Semanal: minute hour * * dayOfWeek (dom is *, dow is not *)
    if (dom === "*" && month === "*" && dow !== "*") {
        const [h, m] = [hour, minute];
        return {
            frequency: "weekly",
            values: { time: `${h}:${m}`, day: dow }
        };
    }

    // Mensual: minute hour dayOfMonth * * (dow is *)
    if (dow === "*" && dom !== "*" && month === "*") {
        const [h, m] = [hour, minute];
        return {
            frequency: "monthly",
            values: { time: `${h}:${m}`, day: dom }
        };
    }

    // Diario: minute hour * * *
    if (dom === "*" && month === "*" && dow === "*") {
        const [h, m] = [hour, minute];
        return {
            frequency: "daily",
            values: { time: `${h}:${m}` }
        };
    }

    return { frequency: null, values: {} };
}

// ========================
//  Get human-readable frequency label
// ========================
function getFrequencyLabel(cron) {
    if (!cron) return "-";
    const { frequency, values } = parseCronExpression(cron);

    switch (frequency) {
        case "hourly":
            return `Cada hora (min ${values.minute || "0"})`;
        case "daily":
            return `Diario (${values.time || "00:00"})`;
        case "weekly": {
            const days = ["Domingo", "Lunes", "Martes", "Miércoles", "Jueves", "Viernes", "Sábado"];
            const dayName = days[parseInt(values.day)] || "";
            return `Semanal (${dayName} ${values.time || "00:00"})`;
        }
        case "monthly":
            return `Mensual (Día ${values.day}, ${values.time || "00:00"})`;
        default:
            return cron; // fallback to raw cron if unparseable
    }
}

// ========================
//  Format date for display
// ========================
function formatDate(dateString) {
    if (!dateString) return "-";
    const d = new Date(dateString);
    if (isNaN(d.getTime())) return "-";
    return d.toLocaleString('es-ES', {
        year: 'numeric',
        month: '2-digit',
        day: '2-digit',
        hour: '2-digit',
        minute: '2-digit'
    });
}

// ========================
//  Load all schedules from API
// ========================
async function loadSchedules() {
    const loading = document.getElementById("loading");
    const tableContainer = document.getElementById("tableContainer");
    const errorMessage = document.getElementById("errorMessage");

    errorMessage.textContent = "";
    loading.classList.add("visible");
    tableContainer.style.display = "none";

    try {
        const res = await authenticatedFetch(`${API_BASE}/api/schedules`);

        if (!res.ok) throw new Error(`HTTP ${res.status}`);

        schedules = await res.json();
        renderSchedulesTable(schedules);
        tableContainer.style.display = "block";
    } catch (error) {
        errorMessage.textContent = `Error cargando programaciones: ${error.message}`;
    } finally {
        loading.classList.remove("visible");
    }
}

// ========================
//  Render schedules table
// ========================
function renderSchedulesTable(data) {
    const tbody = document.getElementById("schedulesBody");
    tbody.innerHTML = "";

    if (!data || data.length === 0) {
        tbody.innerHTML = "<tr><td colspan='5' style='text-align:center;'>Sin programaciones configuradas</td></tr>";
        return;
    }

    data.forEach(schedule => {
        const tr = document.createElement("tr");

        // Params
        const paramsCell = document.createElement("td");
        paramsCell.textContent = schedule.params || "-";
        paramsCell.title = schedule.params || "";
        tr.appendChild(paramsCell);

        // Frequency label (human-readable)
        const cronCell = document.createElement("td");
        cronCell.textContent = getFrequencyLabel(schedule.cronExpression);
        tr.appendChild(cronCell);

        // IsActive status badge
        const statusCell = document.createElement("td");
        const statusBadge = document.createElement("span");
        statusBadge.className = "status-badge";
        statusBadge.textContent = schedule.isActive ? "Activo" : "Inactivo";
        statusBadge.classList.add(schedule.isActive ? "status-active" : "status-inactive");
        statusCell.appendChild(statusBadge);
        tr.appendChild(statusCell);

        // CreatedAt
        const createdCell = document.createElement("td");
        createdCell.textContent = formatDate(schedule.createdAt);
        tr.appendChild(createdCell);

        // Actions
        const actionsCell = document.createElement("td");
        const actionWrapper = document.createElement("div");
        actionWrapper.className = "action-buttons";

        // Edit button
        const editBtn = document.createElement("button");
        editBtn.className = "btn btn-primary";
        editBtn.textContent = "Editar";
        editBtn.onclick = () => openEditModal(schedule);
        actionWrapper.appendChild(editBtn);

        // Toggle active/inactive button
        const toggleBtn = document.createElement("button");
        toggleBtn.className = "btn btn-secondary";
        toggleBtn.textContent = schedule.isActive ? "Desactivar" : "Activar";
        toggleBtn.onclick = () => toggleSchedule(schedule.id);
        actionWrapper.appendChild(toggleBtn);

        // Delete button
        const deleteBtn = document.createElement("button");
        deleteBtn.className = "btn btn-danger";
        deleteBtn.textContent = "Eliminar";
        deleteBtn.onclick = () => deleteSchedule(schedule.id, schedule.params);
        actionWrapper.appendChild(deleteBtn);

        actionsCell.appendChild(actionWrapper);
        tr.appendChild(actionsCell);
        tbody.appendChild(tr);
    });
}

// ========================
//  Open create modal
// ========================
function openCreateModal() {
    document.getElementById("modalTitle").textContent = "Nueva Programación";
    document.getElementById("scheduleId").value = "";
    document.getElementById("params").value = "";
    document.getElementById("isActive").checked = true;
    document.getElementById("isActiveGroup").style.display = "none";

    // Reset frequency selection
    selectedFrequency = null;
    document.querySelectorAll(".frequency-card").forEach(card => card.classList.remove("selected"));
    document.querySelectorAll(".frequency-fields").forEach(f => f.classList.remove("visible"));

    // Reset frequency field values to defaults
    document.getElementById("hourlyMinute").value = "0";
    document.getElementById("dailyTime").value = "08:00";
    document.getElementById("weeklyDay").value = "1";
    document.getElementById("weeklyTime").value = "08:00";
    document.getElementById("monthlyDay").value = "1";
    document.getElementById("monthlyTime").value = "08:00";

    document.getElementById("scheduleModal").style.display = "flex";
}

// ========================
//  Open edit modal with existing schedule data
// ========================
function openEditModal(schedule) {
    document.getElementById("modalTitle").textContent = "Editar Programación";
    document.getElementById("scheduleId").value = schedule.id;
    document.getElementById("params").value = schedule.params || "";
    document.getElementById("isActive").checked = schedule.isActive;
    document.getElementById("isActiveGroup").style.display = "block";

    // Parse existing cron expression and populate frequency UI
    const parsed = parseCronExpression(schedule.cronExpression);

    if (parsed.frequency) {
        selectFrequency(parsed.frequency);

        // Fill in the parsed values
        if (parsed.values.minute !== undefined) {
            document.getElementById("hourlyMinute").value = parsed.values.minute;
        }
        if (parsed.values.time) {
            document.getElementById("dailyTime").value = parsed.values.time;
            document.getElementById("weeklyTime").value = parsed.values.time;
            document.getElementById("monthlyTime").value = parsed.values.time;
        }
        if (parsed.values.day !== undefined) {
            if (parsed.frequency === "weekly") {
                document.getElementById("weeklyDay").value = parsed.values.day;
            } else if (parsed.frequency === "monthly") {
                document.getElementById("monthlyDay").value = parsed.values.day;
            }
        }
    }

    document.getElementById("scheduleModal").style.display = "flex";
}

// ========================
//  Close modal
// ========================
function closeModal() {
    document.getElementById("scheduleModal").style.display = "none";
    document.getElementById("scheduleForm").reset();

    // Reset frequency state
    selectedFrequency = null;
    document.querySelectorAll(".frequency-card").forEach(card => card.classList.remove("selected"));
    document.querySelectorAll(".frequency-fields").forEach(f => f.classList.remove("visible"));
}

// ========================
//  Save schedule (create or update)
// ========================
async function saveSchedule(event) {
    event.preventDefault();

    // Validate frequency selection
    if (!selectedFrequency) {
        showToast("Seleccione una frecuencia de ejecución", "error");
        return;
    }

    const cronExpression = generateCronExpression();
    if (!cronExpression) {
        showToast("Error generando la expresión de programación", "error");
        return;
    }

    const id = document.getElementById("scheduleId").value;
    const params = document.getElementById("params").value.trim() || null;
    const isActive = document.getElementById("isActive").checked;

    const saveBtn = document.getElementById("saveBtn");
    saveBtn.disabled = true;
    saveBtn.textContent = "Guardando...";

    try {
        let res;
        if (id) {
            // Update existing schedule
            res = await authenticatedFetch(`${API_BASE}/api/schedules/${id}`, {
                method: "PUT",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({
                    params,
                    cronExpression,
                    isActive
                })
            });
        } else {
            // Create new schedule
            res = await authenticatedFetch(`${API_BASE}/api/schedules`, {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({
                    params,
                    cronExpression
                })
            });
        }

        if (!res.ok) {
            const err = await res.json().catch(() => ({}));
            throw new Error(err.message || `HTTP ${res.status}`);
        }

        showToast(id ? "Programación actualizada correctamente" : "Programación creada correctamente", "success");
        closeModal();
        await loadSchedules();
    } catch (error) {
        showToast(`Error: ${error.message}`, "error");
    } finally {
        saveBtn.disabled = false;
        saveBtn.textContent = "Guardar";
    }
}

// ========================
//  Toggle schedule active/inactive state
// ========================
async function toggleSchedule(id) {
    try {
        const res = await authenticatedFetch(`${API_BASE}/api/schedules/${id}/toggle`, {
            method: "PATCH"
        });

        if (!res.ok) {
            const err = await res.json().catch(() => ({}));
            throw new Error(err.message || `HTTP ${res.status}`);
        }

        const updated = await res.json();
        showToast(`Programación ${updated.isActive ? "activada" : "desactivada"} correctamente`, "success");
        await loadSchedules();
    } catch (error) {
        showToast(`Error: ${error.message}`, "error");
    }
}

// ========================
//  Delete schedule
// ========================
async function deleteSchedule(id, params) {
    if (!confirm(`¿Está seguro de eliminar la programación "${params || 'sin parametros'}"?`)) {
        return;
    }

    try {
        const res = await authenticatedFetch(`${API_BASE}/api/schedules/${id}`, {
            method: "DELETE"
        });

        if (!res.ok) {
            const err = await res.json().catch(() => ({}));
            throw new Error(err.message || `HTTP ${res.status}`);
        }

        showToast("Programación eliminada correctamente", "success");
        await loadSchedules();
    } catch (error) {
        showToast(`Error: ${error.message}`, "error");
    }
}

// ========================
//  Toast notification
// ========================
function showToast(message, type) {
    const toast = document.getElementById("toast");
    toast.textContent = message;
    toast.className = "toast visible";

    if (type === "success") {
        toast.classList.add("toast-success");
    } else {
        toast.classList.add("toast-error");
    }

    setTimeout(() => {
        toast.classList.remove("visible");
    }, 4000);
}

// ========================
//  Close modal on overlay click
// ========================
document.getElementById("scheduleModal").addEventListener("click", function (e) {
    if (e.target === this) {
        closeModal();
    }
});

// ========================
//  Close modal on Escape key
// ========================
document.addEventListener("keydown", function (e) {
    if (e.key === "Escape") {
        closeModal();
    }
});

// ========================
//  On page load: check if authenticated
// ========================
(async function init() {
    const authenticated = await ensureAuthenticated();
    if (!authenticated) {
        clearTokens();
        window.location.href = "login.html";
        return;
    }

    // Admin-only guard: redirect non-admin users away from the scheduler
    if (!isAdmin()) {
        window.location.href = "index.html";
        return;
    }

    // Populate user greeting
    const userNameEl = document.getElementById("userName");
    if (userNameEl) {
        userNameEl.textContent = getFirstname() || "Usuario";
    }

    await loadSchedules();
    scheduleAutoRefresh();
    renderFooter();
})();
