defmodule DarkWorldsServer.Engine do
  @moduledoc """
  Game Engine Supervisor
  """
  use DynamicSupervisor

  alias DarkWorldsServer.Engine.Runner

  def start_link(args) do
    DynamicSupervisor.start_link(__MODULE__, args, name: __MODULE__)
  end

  def start_child() do
    DynamicSupervisor.start_child(__MODULE__, Runner)
  end

  @impl true
  def init(_opts) do
    DynamicSupervisor.init(strategy: :one_for_one)
  end

  def list_runners_pids() do
    __MODULE__
    |> DynamicSupervisor.which_children()
    |> Enum.filter(fn children ->
      case children do
        {:undefined, pid, :worker, [Runner]} when is_pid(pid) -> true
        _ -> false
      end
    end)
    |> Enum.map(fn {_, pid, _, _} -> pid end)
  end

  def list_games_ids() do
    list_runners_pids()
    |> Enum.map(fn pid -> Runner.pid_to_game_id(pid) end)
  end
end
