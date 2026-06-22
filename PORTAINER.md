# Portainer Installation

This repository is prepared for deployment as a Portainer Git stack.

## Deploy From Git

1. Open Portainer.
2. Go to **Stacks**.
3. Choose **Add stack**.
4. Select **Repository**.
5. Repository URL:

   ```text
   https://github.com/sandavdesigns/rdpman.git
   ```

6. Compose path:

   ```text
   portainer-stack.yml
   ```

7. Add these environment variables in Portainer:

   ```text
   RDP_MAN_SECRET_KEY=replace-with-a-long-random-secret
   RDP_MAN_ADMIN_USERNAME=admin
   RDP_MAN_ADMIN_PASSWORD=replace-with-a-strong-password
   RDP_MAN_API_TOKEN=replace-with-a-long-random-api-token
   ```

8. Deploy the stack.
9. Open:

   ```text
   http://<docker-host>:8095
   ```

## Important

- Keep `RDP_MAN_SECRET_KEY` stable. Existing encrypted credentials depend on it.
- `/app/data` is persisted in the Docker volume `rdpman_data`.
- The service exposes port `8095` on the Docker host and port `8000` inside the container.
- Use HTTPS or a reverse proxy before exposing this service outside your internal network.

## Updating

In Portainer, pull and redeploy the stack from the same Git repository. The SQLite database remains in the `rdpman_data` volume.

