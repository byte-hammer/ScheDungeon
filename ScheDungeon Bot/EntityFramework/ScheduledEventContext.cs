﻿using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScheDungeon.EntityFramework
{
    internal class ScheduledEventContext : DbContext
    {
        private const string DB_DEVELOPER_NAME = "ByteHammer";
        private const string DB_DIR_NAME = "ScheDungeon";

        public string DbPath { get; }

        public DbSet<ScheduledEvent> ScheduledEvents { get; set; }
        public DbSet<Player> Players { get; set; }
        public DbSet<Session> Sessions { get; set; }

        public ScheduledEventContext()
        {
            var folder = Environment.SpecialFolder.LocalApplicationData;
            var path = Environment.GetFolderPath(folder);
            DbPath = Path.Join(path, DB_DEVELOPER_NAME, DB_DIR_NAME, "ScheDungeon.db");
        }

        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            options.UseSqlite($"Data Source={DbPath}");
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // manual changes to the model go here
        }
    }

    public class ScheduledEvent
    {
        public int Id { get; set; }
        public string Name { get; set; } = "Unnamed Event";
        public string Description { get; set; } = "";

        public ICollection<Player> Players { get; } = new List<Player>();
        public ICollection<Session> Sessions { get; } = new List<Session>();
    }

    public class Player
    {
        public int Id { get; set; }
        public string Name { get; set; } = "Unspecified Player";

        public int? ScheduledEventId { get; set; }
        public ScheduledEvent? ScheduledEvent { get; set; } = null!;
    }

    public class Session
    {
        public int Id { get; set; }
        public int StartTime { get; set; } // Stored as Unix Epoch timestamp
        public bool Triggered { get; set; } 
        
        public int? ScheduledEventId { get; set; }
        public ScheduledEvent? ScheduledEvent { get; set; } = null!;
    }
}
