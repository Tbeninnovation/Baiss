using System;

namespace Baiss.Application.DTOs;

public class UpdateTreeStructureScheduleDto
{
    public string Schedule { get; set; } = "0 0 0 * * ?";
    public bool Enabled { get; set; }
}
