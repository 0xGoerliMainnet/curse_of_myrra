use crate::time_utils::time_now;
use rustler::NifStruct;

/*
    Note: To track cooldowns we are storing the last system time when the ability/attack
    was used. This is not ideal, because system time is unreliable, but storing an `Instant`
    as a field on players does not work because it can't be encoded between Elixir and Rust.
*/

#[derive(Debug, Clone, NifStruct)]
#[module = "DarkWorldsServer.Engine.Player"]
pub struct Player {
    pub id: u64,
    pub health: u64,
    pub position: (usize, usize),
    /// Time of the last melee attack done by the player, measured in seconds.
    pub last_melee_attack: u64,
}

impl Player {
    pub fn new(id: u64, health: u64, position: (usize, usize)) -> Self {
        Self {
            id,
            health,
            position,
            last_melee_attack: time_now(),
        }
    }
}
