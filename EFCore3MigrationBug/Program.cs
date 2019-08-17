using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EFCore3MigrationBug
{
	class Program
	{
		public static async Task Main(string[] args)
		{
			var services = new ServiceCollection();

			services.AddLogging(logging => logging.AddConsole());
			services.AddDbContext<ApplicationContext>(db =>
			{
				db.UseSqlServer(@"Server=(localdb)\mssqllocaldb; Database=test; Trusted_Connection=True; MultipleActiveResultSets=true")
					.EnableDetailedErrors()
					.EnableSensitiveDataLogging();
			});

			using var root = services.BuildServiceProvider();

			using (var scope = root.CreateScope())
			{
				var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
				await db.Database.EnsureDeletedAsync();
				await db.Database.EnsureCreatedAsync();
			}

			using (var scope = root.CreateScope())
			{
				var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
			}

			Console.ReadKey();
		}
	}

	// Define other methods and classes here
	public class ApplicationContext : DbContext
	{
		public ApplicationContext(DbContextOptions<ApplicationContext> options)
			: base(options)
		{
		}

		public DbSet<Blog> Blogs { get; set; }

		protected override void OnModelCreating(ModelBuilder model)
		{
			model.ApplyConfigurationsFromAssembly(this.GetType().Assembly);
		}
	}

	public sealed class Blog
	{
		public Guid BlogId { get; set; }
		public String Name { get; set; }

		public ICollection<Post> Posts { get; set; }

		public sealed class Post
		{
			public Guid BlogId { get; set; }
			public Guid PostId { get; set; }
			public String Name { get; set; }

			public ICollection<Attachment> Attachments { get; set; } = new HashSet<Attachment>();

			public sealed class Attachment
			{
				public Guid BlogId { get; set; }
				public Guid PostId { get; set; }
				public Guid AttachmentId { get; set; }
				public String Name { get; set; }
			}
		}

		internal sealed class EntityConfiguration : IEntityTypeConfiguration<Blog>
		{
			public void Configure(EntityTypeBuilder<Blog> builder)
			{
				builder.ToTable("Blog");
				builder.HasKey(x => x.BlogId);

				builder.OwnsMany(x => x.Posts, line =>
				{
					line.ToTable("Post");
					line.HasKey(x => new { x.BlogId, x.PostId });
					line.WithOwner().HasForeignKey(x => x.BlogId);

					line.OwnsMany(x => x.Attachments, history =>
					{
						history.ToTable("Attachment");
						history.HasKey(x => new { x.BlogId, x.PostId, x.AttachmentId }).IsClustered();
						history.WithOwner().HasForeignKey(x => new { x.BlogId, x.PostId });
					});
				});
			}
		}
	}

	public class ApplicationContextFactory : IDesignTimeDbContextFactory<ApplicationContext>
	{
		public ApplicationContext CreateDbContext(string[] args)
		{
			var db = new DbContextOptionsBuilder<ApplicationContext>();
			db.UseSqlServer(@"Server=(localdb)\mssqllocaldb; Database=test; Trusted_Connection=True; MultipleActiveResultSets=true")
				.EnableDetailedErrors()
				.EnableSensitiveDataLogging();

			return new ApplicationContext(db.Options);
		}
	}
}
