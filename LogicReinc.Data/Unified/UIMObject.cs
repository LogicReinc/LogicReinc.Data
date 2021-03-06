﻿using LogicReinc.Collection;
using LogicReinc.Collections;
using LogicReinc.Data.Unified.Attributes;
using LogicReinc.Expressions;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace LogicReinc.Data.Unified
{
    /// <summary>
    /// Unified In-Memory Object
    /// </summary>
    /// <typeparam name="T">Inheritted type</typeparam>
    public class UnifiedIMObject<T> : IUnifiedIMObject where T : UnifiedIMObject<T>
    {
        protected static bool initializing = false;
        protected static bool loaded = false;
        public static bool Loaded { get { return loaded; } }

        private static UnifiedDatabaseProvider provider;
        public static UnifiedDatabaseProvider Provider { get { return provider; } }

        public static string DatabaseName { get { return Provider.DatabaseName; } }

        protected static TSList<T> database;
        public static TSList<T> Database
        {
            get
            {
                if (database == null)
                {
                    if (!Loaded)
                        new UnifiedIMObject<T>().Load();
                    InitializeDatabase();
                }
                return database;
            }
        }

        protected static ObjectIndex<IUnifiedIMObject> Index
        {
            get
            {
                if (!UnifiedSystem.AllowIndexes)
                    throw new Exception("Indexes for the Unified framework are disabled");
                if (!UnifiedSystem.Indexes.ContainsKey(typeof(T)))
                    throw new KeyNotFoundException("This type has no registered indexes");
                return UnifiedSystem.Indexes[typeof(T)];
            }
        }

        internal override Type DataType => typeof(T);


        internal override IList DatabaseBase => (IList)Database;
        internal override Dictionary<string, UIMPropertyState> PropertyStates { get; } = new Dictionary<string, UIMPropertyState>();
        internal override List<KeyValuePair<UnifiedIMReference, IUnifiedIMObject>> RefTo { get; } = new List<KeyValuePair<UnifiedIMReference, IUnifiedIMObject>>();

        protected List<KeyValuePair<UnifiedIMReference, IUnifiedIMObject>> ReferenceTo => RefTo;

        
        public override string ObjectID { get; set; }


        private static void InitializeDatabase()
        {
            database = new TSList<T>(Provider.GetAllObjects<T>());

            database.ForEach((x) =>
            {
                UnifiedSystem.HandleObjectCreation<T>(x);
            });

            loaded = true;
        }

        public virtual bool Load()
        {
            if (!initializing)
            {
                initializing = true;
                bool b = Provider.LoadCollection<T>();
                UnifiedSystem.RegisterType(typeof(T));
                if (b && !loaded)
                    InitializeDatabase();
                return b;
            }
            return true;
        }

        public virtual bool Update()
        {
            if (!Loaded)
                Load();

            UnifiedSystem.HandleObjectChange<T>(this);
            return Provider.UpdateObject<T>((T)this);
        }
        public virtual bool Update(T obj, bool update, params string[] properties)
        {
            Type t = GetType();
            if (properties != null && properties.Length > 0)
            {
                foreach (string s in properties)
                {
                    PropertyInfo type = t.GetProperty(s);
                    if (type.SetMethod != null)
                        Property.Set(this, type.Name, Property.Get(obj, type.Name));
                }
            }
            else
            {
                foreach (PropertyInfo prop in t.GetProperties())
                {
                    if (prop.SetMethod != null)
                        Property.Set(this, prop.Name, Property.Get(obj, prop.Name));
                        //prop.SetValue(this, prop.GetValue(obj));
                }
            }
            if (update)
                return Update();
            return true;
        }
        public virtual bool UpdateProperties(params string[] properties)
        {
            if (!Loaded)
                Load();
            return Provider.UpdateProperties<T>(ObjectID, (T)this, properties);
        }

        public virtual bool Insert()
        {
            if (!Loaded)
                Load();
            bool result = Provider.InsertObject<T>((T)this);
            if (result)
                database.Add((T)this);

            UnifiedSystem.HandleObjectCreation<T>(this);

            return result;
        }

        public virtual bool Delete()
        {
            if (!Loaded)
                Load();
            bool result = Provider.DeleteObject<T>(ObjectID);
            if (result)
                database.Remove((T)this);

            UnifiedSystem.HandleObjectDeletion<T>(this);

            return result;
        }



        public static T GetObject(string id)
        {
            if (id == null)
                return null;
            if (UnifiedSystem.UseOmniBase)
            {
                lock (UnifiedSystem.OmniBase)
                    if (UnifiedSystem.OmniBase.ContainsKey(id))
                        return (T)UnifiedSystem.OmniBase[id];
                    else
                        return null;
            }
            else
                return Database.FirstOrDefault(x => x.ObjectID == id);

        }

        public static List<T> WhereIndexed(string property, object value)
        {
            return Index.GetIndex(property, value).Cast<T>().ToList();
        }

        //Utility
        public static void SetProvider(UnifiedDatabaseProvider p, bool loadDatabase = false)
        {
            provider = p;
            Activator.CreateInstance<T>().Load();
        }
    }
}
