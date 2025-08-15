using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PeptideDataHomogenizer.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreateSqlLite : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Articles",
                columns: table => new
                {
                    doi = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    PubMedId = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 600, nullable: false),
                    Journal = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Authors = table.Column<string>(type: "TEXT", nullable: false),
                    Abstract = table.Column<string>(type: "TEXT", nullable: false),
                    PublicationDate = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Articles", x => x.doi);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    first_name = table.Column<string>(type: "TEXT", nullable: false),
                    last_name = table.Column<string>(type: "TEXT", nullable: false),
                    title = table.Column<string>(type: "TEXT", nullable: false),
                    registration_token = table.Column<long>(type: "INTEGER", maxLength: 500, nullable: true),
                    registration_token_expiration = table.Column<DateTime>(type: "TEXT", nullable: true),
                    has_registered = table.Column<bool>(type: "INTEGER", nullable: false),
                    UserName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "INTEGER", nullable: false),
                    PasswordHash = table.Column<string>(type: "TEXT", nullable: true),
                    SecurityStamp = table.Column<string>(type: "TEXT", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "TEXT", nullable: true),
                    PhoneNumber = table.Column<string>(type: "TEXT", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "INTEGER", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ForceFieldsSoftware",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    software_name = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ForceFieldsSoftware", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "Ions",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ion_name = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "Organizations",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    name = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    logo_data = table.Column<byte[]>(type: "BLOB", maxLength: 26214400, nullable: true),
                    content_type = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    website_url = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Organizations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "SimulationMethods",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    method_name = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SimulationMethods", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "SimulationSoftware",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    software_name = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SimulationSoftware", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "UsersPerProjects",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    project_id = table.Column<int>(type: "INTEGER", nullable: false),
                    user_id = table.Column<string>(type: "TEXT", nullable: false),
                    role = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsersPerProjects", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WaterModels",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    water_model_name = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    water_model_type = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WaterModels", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "Chapters",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    title = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    content = table.Column<string>(type: "TEXT", maxLength: 2147483647, nullable: false),
                    index = table.Column<int>(type: "INTEGER", nullable: false),
                    article_doi = table.Column<string>(type: "nvarchar(255)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Chapters", x => x.id);
                    table.ForeignKey(
                        name: "FK_Chapters_Articles_article_doi",
                        column: x => x.article_doi,
                        principalTable: "Articles",
                        principalColumn: "doi",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Images",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    caption = table.Column<string>(type: "TEXT", nullable: false),
                    image_data = table.Column<byte[]>(type: "BLOB", maxLength: 26214400, nullable: false),
                    file_name = table.Column<string>(type: "TEXT", nullable: false),
                    content_type = table.Column<string>(type: "TEXT", nullable: false),
                    article_doi = table.Column<string>(type: "nvarchar(255)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Images", x => x.id);
                    table.ForeignKey(
                        name: "FK_Images_Articles_article_doi",
                        column: x => x.article_doi,
                        principalTable: "Articles",
                        principalColumn: "doi",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Tables",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    caption = table.Column<string>(type: "TEXT", nullable: false),
                    tableJson = table.Column<string>(type: "TEXT", nullable: false),
                    article_doi = table.Column<string>(type: "nvarchar(255)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tables", x => x.id);
                    table.ForeignKey(
                        name: "FK_Tables_Articles_article_doi",
                        column: x => x.article_doi,
                        principalTable: "Articles",
                        principalColumn: "doi",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RoleId = table.Column<string>(type: "TEXT", nullable: false),
                    ClaimType = table.Column<string>(type: "TEXT", nullable: true),
                    ClaimValue = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    ClaimType = table.Column<string>(type: "TEXT", nullable: true),
                    ClaimValue = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "TEXT", nullable: false),
                    ProviderKey = table.Column<string>(type: "TEXT", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "TEXT", nullable: true),
                    UserId = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    RoleId = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    LoginProvider = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Value = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Projects",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    name = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    logo_data = table.Column<byte[]>(type: "BLOB", maxLength: 26214400, nullable: true),
                    content_type = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    organization_id = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Projects", x => x.id);
                    table.ForeignKey(
                        name: "FK_Projects_Organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "Organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UsersPerOrganizations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    organization_id = table.Column<int>(type: "INTEGER", nullable: false),
                    user_id = table.Column<string>(type: "TEXT", nullable: false),
                    role = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsersPerOrganizations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UsersPerOrganizations_Organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "Organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ArticlePerProjects",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProjectId = table.Column<int>(type: "INTEGER", nullable: false),
                    ArticleId = table.Column<string>(type: "nvarchar(255)", nullable: false),
                    IsDiscredited = table.Column<bool>(type: "INTEGER", nullable: false),
                    DiscreditedReason = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    IsApproved = table.Column<bool>(type: "INTEGER", nullable: false),
                    datetime_approval = table.Column<DateTime>(type: "TEXT", nullable: true),
                    approved_by = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArticlePerProjects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArticlePerProjects_Articles_ArticleId",
                        column: x => x.ArticleId,
                        principalTable: "Articles",
                        principalColumn: "doi",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ArticlePerProjects_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DiscreditedJournals",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    title = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    project_id = table.Column<int>(type: "INTEGER", nullable: false),
                    discredited_reason = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    discredited_by = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiscreditedJournals", x => x.id);
                    table.ForeignKey(
                        name: "FK_DiscreditedJournals_Projects_project_id",
                        column: x => x.project_id,
                        principalTable: "Projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DiscreditedPublishers",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    url = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    project_id = table.Column<int>(type: "INTEGER", nullable: false),
                    discredited_reason = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    discredited_by = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiscreditedPublishers", x => x.id);
                    table.ForeignKey(
                        name: "FK_DiscreditedPublishers_Projects_project_id",
                        column: x => x.project_id,
                        principalTable: "Projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProteinData",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    protein_id = table.Column<string>(type: "TEXT", nullable: false),
                    classification = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    organism = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    method = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    residue = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    binder = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    software_name = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    software_version = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    water_model = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    water_model_type = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    force_field = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    simulation_method = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    temperature = table.Column<double>(type: "REAL", nullable: false),
                    ions = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    ion_concentration = table.Column<double>(type: "REAL", nullable: false),
                    simulation_length = table.Column<double>(type: "REAL", nullable: false),
                    Kd = table.Column<double>(type: "REAL", nullable: false),
                    KOff = table.Column<double>(type: "REAL", nullable: false),
                    KOn = table.Column<double>(type: "REAL", nullable: false),
                    free_binding_energy = table.Column<double>(type: "REAL", nullable: false),
                    ProjectId = table.Column<int>(type: "INTEGER", nullable: false),
                    ArticleDoi = table.Column<string>(type: "nvarchar(255)", nullable: false),
                    Approved = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProteinData", x => x.id);
                    table.ForeignKey(
                        name: "FK_ProteinData_Articles_ArticleDoi",
                        column: x => x.ArticleDoi,
                        principalTable: "Articles",
                        principalColumn: "doi",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProteinData_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ArticlePerProjects_ArticleId",
                table: "ArticlePerProjects",
                column: "ArticleId");

            migrationBuilder.CreateIndex(
                name: "IX_ArticlePerProjects_ProjectId",
                table: "ArticlePerProjects",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Chapters_article_doi",
                table: "Chapters",
                column: "article_doi");

            migrationBuilder.CreateIndex(
                name: "IX_DiscreditedJournals_project_id",
                table: "DiscreditedJournals",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "IX_DiscreditedPublishers_project_id",
                table: "DiscreditedPublishers",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "IX_Images_article_doi",
                table: "Images",
                column: "article_doi");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_organization_id",
                table: "Projects",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "IX_ProteinData_ArticleDoi",
                table: "ProteinData",
                column: "ArticleDoi");

            migrationBuilder.CreateIndex(
                name: "IX_ProteinData_ProjectId",
                table: "ProteinData",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Tables_article_doi",
                table: "Tables",
                column: "article_doi");

            migrationBuilder.CreateIndex(
                name: "IX_UsersPerOrganizations_organization_id",
                table: "UsersPerOrganizations",
                column: "organization_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ArticlePerProjects");

            migrationBuilder.DropTable(
                name: "AspNetRoleClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "Chapters");

            migrationBuilder.DropTable(
                name: "DiscreditedJournals");

            migrationBuilder.DropTable(
                name: "DiscreditedPublishers");

            migrationBuilder.DropTable(
                name: "ForceFieldsSoftware");

            migrationBuilder.DropTable(
                name: "Images");

            migrationBuilder.DropTable(
                name: "Ions");

            migrationBuilder.DropTable(
                name: "ProteinData");

            migrationBuilder.DropTable(
                name: "SimulationMethods");

            migrationBuilder.DropTable(
                name: "SimulationSoftware");

            migrationBuilder.DropTable(
                name: "Tables");

            migrationBuilder.DropTable(
                name: "UsersPerOrganizations");

            migrationBuilder.DropTable(
                name: "UsersPerProjects");

            migrationBuilder.DropTable(
                name: "WaterModels");

            migrationBuilder.DropTable(
                name: "AspNetRoles");

            migrationBuilder.DropTable(
                name: "AspNetUsers");

            migrationBuilder.DropTable(
                name: "Projects");

            migrationBuilder.DropTable(
                name: "Articles");

            migrationBuilder.DropTable(
                name: "Organizations");
        }
    }
}
