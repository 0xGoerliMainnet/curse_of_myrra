defmodule DarkWorldsServer.WsClient do
  use WebSockex
  require Logger
  alias DarkWorldsServer.Communication
  alias DarkWorldsServer.Communication.Proto.ClientAction
  alias DarkWorldsServer.Engine.ActionOk
  alias DarkWorldsServer.Engine.Runner

  @server_hash Application.compile_env(:dark_worlds_server, :information) |> Keyword.get(:version_hash)

  def start_link(url) do
    WebSockex.start_link(url, __MODULE__, %{},
      name: __MODULE__,
      extra_headers: [{"dark-worlds-client-hash", @server_hash}]
    )
  end

  def get_board(session_id) do
    runner_pid = Communication.external_id_to_pid(session_id)
    GenServer.call(runner_pid, :get_board)
  end

  def get_players(session_id) do
    runner_pid = Communication.external_id_to_pid(session_id)
    GenServer.call(runner_pid, :get_players)
  end

  def set_character_muflus(player_id, session_id) do
    runner_pid = Communication.external_id_to_pid(session_id)

    Runner.play(runner_pid, player_id, %ActionOk{
      action: :select_character,
      value: %{player_id: player_id, character_name: "Muflus"},
      timestamp: nil
    })
  end

  def get_character_speed(session_id) do
    runner_pid = Communication.external_id_to_pid(session_id)
    GenServer.call(runner_pid, :get_character_speed)
  end

  def move(player, :up), do: _move(player, :UP)
  def move(player, :down), do: _move(player, :DOWN)
  def move(player, :left), do: _move(player, :LEFT)
  def move(player, :right), do: _move(player, :RIGHT)

  def move_with_joystick(player, :up), do: _move_with_joystick(player, %{x: -1, y: 0})
  def move_with_joystick(player, :down), do: _move_with_joystick(player, %{x: 1, y: 0})
  def move_with_joystick(player, :left), do: _move_with_joystick(player, %{x: 0, y: -1})
  def move_with_joystick(player, :right), do: _move_with_joystick(player, %{x: 0, y: 1})

  def attack(player, :up), do: _attack(player, :UP)
  def attack(player, :down), do: _attack(player, :DOWN)
  def attack(player, :left), do: _attack(player, :LEFT)
  def attack(player, :right), do: _attack(player, :RIGHT)

  def attack_aoe(player, position) do
    %{
      "player" => player,
      "action" => "skill_1",
      "value" => %{"x" => position.x, "y" => position.y}
    }
    |> send_command()
  end

  def basic_attack(player, position) do
    %{
      "player" => player,
      "action" => "basic_attack",
      "value" => %{"x" => position.x, "y" => position.y}
    }
    |> send_command()
  end

  defp _move(_player, direction) do
    %ClientAction{action: :MOVE, direction: direction}
    |> send_command()
  end

  # def teleport(player, position) do
  #   %{
  #     "player" => player,
  #     "action" => "teleport",
  #     "value" => %{"x" => position.x, "y" => position.y}
  #   }

  defp _move_with_joystick(_player, %{x: x, y: y}) do
    %ClientAction{action: :MOVE_WITH_JOYSTICK, move_delta: %{x: x, y: y}}
    |> send_command()
  end

  defp _attack(_player, direction) do
    %ClientAction{action: :MOVE, direction: direction}
    |> send_command()
  end

  def handle_frame({_type, _msg}, state) do
    {:ok, state}
  end

  def handle_cast({:send, {_type, msg} = frame}, state) do
    Logger.info("Sending frame with payload: #{msg}")
    {:reply, frame, state}
  end

  defp send_command(command) do
    pid = Process.whereis(__MODULE__)
    WebSockex.cast(pid, {:send, {:binary, ClientAction.encode(command)}})
  end
end
