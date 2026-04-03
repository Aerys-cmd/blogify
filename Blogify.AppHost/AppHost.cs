var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithPgAdmin();
    
var blogDb = postgres.AddDatabase("blogdb");

var webProject = builder.AddProject<Projects.Blogify_Web>("blogify-web")
    .WithHttpEndpoint(port: 5050, name: "fixed-web")
    .WithReference(blogDb);

builder.AddContainer("traefik", "traefik", "v3.0")
    .WithBindMount("traefik.yml", "/etc/traefik/dynamic.yml")
    .WithArgs("--providers.file.filename=/etc/traefik/dynamic.yml", "--api.insecure=true", "--entrypoints.web.address=:80")
    .WithHttpEndpoint(port: 8080, targetPort: 80, name: "traefik-http");

builder.Build().Run();
