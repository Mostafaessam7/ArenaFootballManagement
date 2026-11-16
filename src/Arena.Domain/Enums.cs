namespace Arena.Domain;

public enum UserRole { Admin, Organizer, TeamManager, Spectator, GateStaff }
public enum TournamentStatus { Draft, RegistrationOpen, InProgress, Completed }
public enum EventStatus { Scheduled, Finished, Cancelled }
public enum BookingStatus { Confirmed, Cancelled }
