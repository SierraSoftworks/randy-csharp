using System;
using System.Globalization;
using System.Linq;
using System.Xml.Serialization;
using SierraLib.API.Views;

namespace Rex.Models
{
    public partial class Idea
    {
        [XmlType("Idea")]
        public class Version3 : IView<Idea>
        {
            [XmlAttribute("CollectionId")]
            public string Collection { get; set; }

            [XmlAttribute("Id")]
            public string Id { get; set; }

            [XmlElement("Name")]
            public string Name { get; set; }

            [XmlElement("Description")]
            public string Description { get; set; }

            [XmlAttribute("Completed")]
            public bool? Completed { get; set; }

            [XmlArray("Tags")]
            [XmlArrayItem("Tag")]
            [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "This models the API response.")]
            public string[] Tags { get; set; }

            public class Representer : IRepresenter<Idea, Version3>
            {
                public Idea ToModel(Version3 view)
                {
                    if (view is null)
                    {
                        throw new ArgumentNullException(nameof(view));
                    }

                    return new Idea
                    {
                        CollectionId = view.Collection != null ? Guid.Parse(view.Collection) : Guid.Empty,
                        Id = view.Id != null ? Guid.Parse(view.Id) : Guid.NewGuid(),
                        Name = view.Name,
                        Description = view.Description,
                        Completed = view.Completed ?? false,
                        Tags = new System.Collections.Generic.HashSet<string>(view.Tags ?? Array.Empty<string>()),
                    };
                }

                public Version3 ToView(Idea model)
                {
                    if (model is null)
                    {
                        throw new ArgumentNullException(nameof(model));
                    }

                    return new Version3
                    {
                        Collection = model.CollectionId.ToString("N", CultureInfo.InvariantCulture),
                        Id = model.Id.ToString("N", CultureInfo.InvariantCulture),
                        Name = model.Name,
                        Description = model.Description,
                        Completed = model.Completed,
                        Tags = model.Tags.ToArray(),
                    };
                }
            }
        }
    }
}