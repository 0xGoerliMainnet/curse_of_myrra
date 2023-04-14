# This file is responsible for configuring your application
# and its dependencies with the aid of the Config module.
#
# This configuration file is loaded before any dependency and
# is restricted to this project.

# General application configuration
import Config

# Configures the endpoint
dispatch = [
  _: [
    {"/play", DarkWorldsServerWeb.PlayWebSocket, []},
    {:_, Phoenix.Endpoint.Cowboy2Handler, {DarkWorldsServerWeb.Endpoint, []}}
  ]
]

config :dark_worlds_server, DarkWorldsServerWeb.Endpoint,
  url: [host: "localhost"],
  render_errors: [
    formats: [json: DarkWorldsServerWeb.ErrorJSON],
    layout: false
  ],
  pubsub_server: DarkWorldsServer.PubSub,
  live_view: [signing_salt: "HPijD5SN"],
  http: [dispatch: dispatch]

# Configures the mailer
#
# By default it uses the "Local" adapter which stores the emails
# locally. You can see the emails in your browser, at "/dev/mailbox".
#
# For production it's recommended to configure a different adapter
# at the `config/runtime.exs`.
config :dark_worlds_server, DarkWorldsServer.Mailer, adapter: Swoosh.Adapters.Local

# Configures Elixir's Logger
config :logger, :console,
  format: "$time $metadata[$level] $message\n",
  metadata: [:request_id]

# Use Jason for JSON parsing in Phoenix
config :phoenix, :json_library, Jason

# Import environment specific config. This must remain at the bottom
# of this file so it overrides the configuration defined above.
import_config "#{config_env()}.exs"
