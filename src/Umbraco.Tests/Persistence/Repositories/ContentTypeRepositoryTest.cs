﻿using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using Umbraco.Core;
using Umbraco.Core.Cache;
using Umbraco.Core.Configuration.Models;
using Umbraco.Core.Models;
using Umbraco.Core.Persistence;
using Umbraco.Core.Persistence.Dtos;
using Umbraco.Core.Persistence.Repositories.Implement;
using Umbraco.Core.PropertyEditors;
using Umbraco.Core.Scoping;
using Umbraco.Tests.Common.Builders;
using Umbraco.Tests.TestHelpers;
using Umbraco.Tests.TestHelpers.Entities;
using Umbraco.Tests.Testing;
using Umbraco.Web.Models.ContentEditing;

namespace Umbraco.Tests.Persistence.Repositories
{
    [TestFixture]
    [UmbracoTest(Mapper = true, Database = UmbracoTestOptions.Database.NewSchemaPerTest)]
    public class ContentTypeRepositoryTest : TestWithDatabaseBase
    {
        private IOptions<GlobalSettings> _globalSettings;

        public override void SetUp()
        {
            base.SetUp();

            CreateTestData();

            _globalSettings = Microsoft.Extensions.Options.Options.Create(new GlobalSettings());
        }

        private DocumentRepository CreateRepository(IScopeAccessor scopeAccessor, out ContentTypeRepository contentTypeRepository)
        {
            var langRepository = new LanguageRepository(scopeAccessor, AppCaches.Disabled, LoggerFactory.CreateLogger<LanguageRepository>(), _globalSettings);
            var templateRepository = new TemplateRepository(scopeAccessor, AppCaches.Disabled, LoggerFactory.CreateLogger<TemplateRepository>(), TestObjects.GetFileSystemsMock(), IOHelper, ShortStringHelper);
            var tagRepository = new TagRepository(scopeAccessor, AppCaches.Disabled, LoggerFactory.CreateLogger<TagRepository>());
            var commonRepository = new ContentTypeCommonRepository(scopeAccessor, templateRepository, AppCaches.Disabled, ShortStringHelper);
            contentTypeRepository = new ContentTypeRepository(scopeAccessor, AppCaches.Disabled, LoggerFactory.CreateLogger<ContentTypeRepository>(), commonRepository, langRepository, ShortStringHelper);
            var languageRepository = new LanguageRepository(scopeAccessor, AppCaches.Disabled, LoggerFactory.CreateLogger<LanguageRepository>(), _globalSettings);
            var relationTypeRepository = new RelationTypeRepository(scopeAccessor, AppCaches.Disabled, LoggerFactory.CreateLogger<RelationTypeRepository>());
            var entityRepository = new EntityRepository(scopeAccessor);
            var relationRepository = new RelationRepository(scopeAccessor, LoggerFactory.CreateLogger<RelationRepository>(), relationTypeRepository, entityRepository);
            var propertyEditors = new Lazy<PropertyEditorCollection>(() => new PropertyEditorCollection(new DataEditorCollection(Enumerable.Empty<IDataEditor>())));
            var dataValueReferences = new DataValueReferenceFactoryCollection(Enumerable.Empty<IDataValueReferenceFactory>());
            var repository = new DocumentRepository(scopeAccessor, AppCaches.Disabled, LoggerFactory.CreateLogger<DocumentRepository>(), LoggerFactory, contentTypeRepository, templateRepository, tagRepository, languageRepository, relationRepository, relationTypeRepository, propertyEditors, dataValueReferences, DataTypeService);
            return repository;
        }

        private ContentTypeRepository CreateRepository(IScopeAccessor scopeAccessor)
        {
            var langRepository = new LanguageRepository(scopeAccessor, AppCaches.Disabled, LoggerFactory.CreateLogger<LanguageRepository>(), _globalSettings);
            var templateRepository = new TemplateRepository(scopeAccessor, AppCaches.Disabled, LoggerFactory.CreateLogger<TemplateRepository>(), TestObjects.GetFileSystemsMock(), IOHelper, ShortStringHelper);
            var commonRepository = new ContentTypeCommonRepository(scopeAccessor, templateRepository, AppCaches.Disabled, ShortStringHelper);
            var contentTypeRepository = new ContentTypeRepository(scopeAccessor, AppCaches.Disabled, LoggerFactory.CreateLogger<ContentTypeRepository>(), commonRepository, langRepository, ShortStringHelper);
            return contentTypeRepository;
        }

        private MediaTypeRepository CreateMediaTypeRepository(IScopeAccessor scopeAccessor)
        {
            var templateRepository = new TemplateRepository(scopeAccessor, AppCaches.Disabled, LoggerFactory.CreateLogger<TemplateRepository>(), TestObjects.GetFileSystemsMock(), IOHelper, ShortStringHelper);
            var commonRepository = new ContentTypeCommonRepository(scopeAccessor, templateRepository, AppCaches.Disabled, ShortStringHelper);
            var langRepository = new LanguageRepository(scopeAccessor, AppCaches.Disabled, LoggerFactory.CreateLogger<LanguageRepository>(), _globalSettings);
            var contentTypeRepository = new MediaTypeRepository(scopeAccessor, AppCaches.Disabled, LoggerFactory.CreateLogger<MediaTypeRepository>(), commonRepository, langRepository, ShortStringHelper);
            return contentTypeRepository;
        }

        private EntityContainerRepository CreateContainerRepository(IScopeAccessor scopeAccessor, Guid containerEntityType)
        {
            return new EntityContainerRepository(scopeAccessor, AppCaches.Disabled, LoggerFactory.CreateLogger<EntityContainerRepository>(), containerEntityType);
        }

        // TODO: Add test to verify SetDefaultTemplates updates both AllowedTemplates and DefaultTemplate(id).

        [Test]
        public void Maps_Templates_Correctly()
        {
            // Arrange
            var provider = TestObjects.GetScopeProvider(LoggerFactory);
            using (var scope = provider.CreateScope())
            {
                var templateRepo = new TemplateRepository((IScopeAccessor) provider, AppCaches.Disabled, LoggerFactory.CreateLogger<TemplateRepository>(), TestObjects.GetFileSystemsMock(), IOHelper, ShortStringHelper);
                var repository = CreateRepository((IScopeAccessor) provider);
                var templates = new[]
                {
                    new Template(ShortStringHelper, "test1", "test1"),
                    new Template(ShortStringHelper, "test2", "test2"),
                    new Template(ShortStringHelper, "test3", "test3")
                };
                foreach (var template in templates)
                {
                    templateRepo.Save(template);
                }


                var contentType = MockedContentTypes.CreateSimpleContentType();
                contentType.AllowedTemplates = new[] { templates[0], templates[1] };
                contentType.SetDefaultTemplate(templates[0]);
                repository.Save(contentType);


                //re-get
                var result = repository.Get(contentType.Id);

                Assert.AreEqual(2, result.AllowedTemplates.Count());
                Assert.AreEqual(templates[0].Id, result.DefaultTemplate.Id);
            }

        }

        [Test]
        public void Can_Move()
        {
            var provider = TestObjects.GetScopeProvider(LoggerFactory);
            using (var scope = provider.CreateScope())
            {
                var containerRepository = CreateContainerRepository((IScopeAccessor) provider, Constants.ObjectTypes.DocumentTypeContainer);
                var repository = CreateRepository((IScopeAccessor) provider);
                var container1 = new EntityContainer(Constants.ObjectTypes.DocumentType) { Name = "blah1" };
                containerRepository.Save(container1);


                var container2 = new EntityContainer(Constants.ObjectTypes.DocumentType) { Name = "blah2", ParentId = container1.Id };
                containerRepository.Save(container2);


                var contentType = (IContentType)MockedContentTypes.CreateBasicContentType("asdfasdf");
                contentType.ParentId = container2.Id;
                repository.Save(contentType);


                //create a
                var contentType2 = (IContentType)new ContentType(ShortStringHelper, contentType, "hello")
                {
                    Name = "Blahasdfsadf"
                };
                contentType.ParentId = contentType.Id;
                repository.Save(contentType2);


                var result = repository.Move(contentType, container1).ToArray();


                Assert.AreEqual(2, result.Count());

                //re-get
                contentType = repository.Get(contentType.Id);
                contentType2 = repository.Get(contentType2.Id);

                Assert.AreEqual(container1.Id, contentType.ParentId);
                Assert.AreNotEqual(result.Single(x => x.Entity.Id == contentType.Id).OriginalPath, contentType.Path);
                Assert.AreNotEqual(result.Single(x => x.Entity.Id == contentType2.Id).OriginalPath, contentType2.Path);
            }

        }

        [Test]
        public void Can_Create_Container()
        {
            var provider = TestObjects.GetScopeProvider(LoggerFactory);
            using (var scope = provider.CreateScope())
            {
                var containerRepository = CreateContainerRepository((IScopeAccessor) provider, Constants.ObjectTypes.DocumentTypeContainer);
                var container = new EntityContainer(Constants.ObjectTypes.DocumentType) { Name = "blah" };
                containerRepository.Save(container);

                Assert.That(container.Id, Is.GreaterThan(0));

                var found = containerRepository.Get(container.Id);
                Assert.IsNotNull(found);
            }
        }

        [Test]
        public void Can_Get_All_Containers()
        {
            EntityContainer container1, container2, container3;

            var provider = TestObjects.GetScopeProvider(LoggerFactory);
            using (var scope = provider.CreateScope())
            {
                var containerRepository = CreateContainerRepository((IScopeAccessor) provider, Constants.ObjectTypes.DocumentTypeContainer);

                container1 = new EntityContainer(Constants.ObjectTypes.DocumentType) { Name = "container1" };
                containerRepository.Save(container1);
                container2 = new EntityContainer(Constants.ObjectTypes.DocumentType) { Name = "container2" };
                containerRepository.Save(container2);
                container3 = new EntityContainer(Constants.ObjectTypes.DocumentType) { Name = "container3" };
                containerRepository.Save(container3);

                Assert.That(container1.Id, Is.GreaterThan(0));
                Assert.That(container2.Id, Is.GreaterThan(0));
                Assert.That(container3.Id, Is.GreaterThan(0));

                var found1 = containerRepository.Get(container1.Id);
                Assert.IsNotNull(found1);
                var found2 = containerRepository.Get(container2.Id);
                Assert.IsNotNull(found2);
                var found3 = containerRepository.Get(container3.Id);
                Assert.IsNotNull(found3);
                var allContainers = containerRepository.GetMany();
                Assert.AreEqual(3, allContainers.Count());
            }
        }

        [Test]
        public void Can_Delete_Container()
        {
            var provider = TestObjects.GetScopeProvider(LoggerFactory);
            using (var scope = provider.CreateScope())
            {
                var containerRepository = CreateContainerRepository((IScopeAccessor) provider, Constants.ObjectTypes.DocumentTypeContainer);
                var container = new EntityContainer(Constants.ObjectTypes.DocumentType) { Name = "blah" };
                containerRepository.Save(container);


                // Act
                containerRepository.Delete(container);


                var found = containerRepository.Get(container.Id);
                Assert.IsNull(found);
            }
        }

        [Test]
        public void Can_Create_Container_Containing_Media_Types()
        {
            var provider = TestObjects.GetScopeProvider(LoggerFactory);
            using (var scope = provider.CreateScope())
            {
                var containerRepository = CreateContainerRepository((IScopeAccessor) provider, Constants.ObjectTypes.MediaTypeContainer);
                var repository = CreateRepository((IScopeAccessor) provider);
                var container = new EntityContainer(Constants.ObjectTypes.MediaType) { Name = "blah" };
                containerRepository.Save(container);


                var contentType = MockedContentTypes.CreateSimpleContentType("test", "Test", propertyGroupName: "testGroup");
                contentType.ParentId = container.Id;
                repository.Save(contentType);


                Assert.AreEqual(container.Id, contentType.ParentId);
            }
        }

        [Test]
        public void Can_Delete_Container_Containing_Media_Types()
        {
            var provider = TestObjects.GetScopeProvider(LoggerFactory);
            using (var scope = provider.CreateScope())
            {
                var containerRepository = CreateContainerRepository((IScopeAccessor) provider, Constants.ObjectTypes.MediaTypeContainer);
                var repository = CreateMediaTypeRepository((IScopeAccessor) provider);
                var container = new EntityContainer(Constants.ObjectTypes.MediaType) { Name = "blah" };
                containerRepository.Save(container);


                IMediaType contentType = MockedContentTypes.CreateSimpleMediaType("test", "Test", propertyGroupName: "testGroup");
                contentType.ParentId = container.Id;
                repository.Save(contentType);


                // Act
                containerRepository.Delete(container);


                var found = containerRepository.Get(container.Id);
                Assert.IsNull(found);

                contentType = repository.Get(contentType.Id);
                Assert.IsNotNull(contentType);
                Assert.AreEqual(-1, contentType.ParentId);
            }
        }

        [Test]
        public void Can_Perform_Add_On_ContentTypeRepository()
        {
            // Arrange
            var provider = TestObjects.GetScopeProvider(LoggerFactory);
            using (var scope = provider.CreateScope())
            {
                var repository = CreateRepository((IScopeAccessor) provider);
                // Act
                var contentType = MockedContentTypes.CreateSimpleContentType("test", "Test", propertyGroupName: "testGroup");
                repository.Save(contentType);


                var fetched = repository.Get(contentType.Id);

                // Assert
                Assert.That(contentType.HasIdentity, Is.True);
                Assert.That(contentType.PropertyGroups.All(x => x.HasIdentity), Is.True);
                Assert.That(contentType.PropertyTypes.All(x => x.HasIdentity), Is.True);
                Assert.That(contentType.Path.Contains(","), Is.True);
                Assert.That(contentType.SortOrder, Is.GreaterThan(0));

                Assert.That(contentType.PropertyGroups.ElementAt(0).Name == "testGroup", Is.True);
                var groupId = contentType.PropertyGroups.ElementAt(0).Id;
                Assert.That(contentType.PropertyTypes.All(x => x.PropertyGroupId.Value == groupId), Is.True);

                foreach (var propertyType in contentType.PropertyTypes)
                {
                    Assert.AreNotEqual(propertyType.Key, Guid.Empty);
                }

                TestHelper.AssertPropertyValuesAreEqual(fetched, contentType, "yyyy-MM-dd HH:mm:ss", ignoreProperties: new [] { "DefaultTemplate", "AllowedTemplates", "UpdateDate" });
            }
        }

        [Test]
        public void Can_Perform_Add_On_ContentTypeRepository_After_Model_Mapping()
        {
            // Arrange
            var provider = TestObjects.GetScopeProvider(LoggerFactory);
            using (var scope = provider.CreateScope())
            {
                var repository = CreateRepository((IScopeAccessor) provider);
                // Act
                var contentType = (IContentType)MockedContentTypes.CreateSimpleContentType2("test", "Test", propertyGroupName: "testGroup");

                Assert.AreEqual(4, contentType.PropertyTypes.Count());

                // remove all templates - since they are not saved, they would break the (!) mapping code
                contentType.AllowedTemplates = new ITemplate[0];

                // there is NO mapping from display to contentType, but only from save
                // to contentType, so if we want to test, let's to it properly!
                var display = Mapper.Map<DocumentTypeDisplay>(contentType);
                var save = MapToContentTypeSave(display);
                var mapped = Mapper.Map<IContentType>(save);

                Assert.AreEqual(4, mapped.PropertyTypes.Count());

                repository.Save(mapped);


                Assert.AreEqual(4, mapped.PropertyTypes.Count());

                //re-get
                contentType = repository.Get(mapped.Id);

                Assert.AreEqual(4, contentType.PropertyTypes.Count());

                // Assert
                Assert.That(contentType.HasIdentity, Is.True);
                Assert.That(contentType.PropertyGroups.All(x => x.HasIdentity), Is.True);
                Assert.That(contentType.PropertyTypes.All(x => x.HasIdentity), Is.True);
                Assert.That(contentType.Path.Contains(","), Is.True);
                Assert.That(contentType.SortOrder, Is.GreaterThan(0));

                Assert.That(contentType.PropertyGroups.ElementAt(0).Name == "testGroup", Is.True);
                var groupId = contentType.PropertyGroups.ElementAt(0).Id;

                var propertyTypes = contentType.PropertyTypes.ToArray();
                Assert.AreEqual("gen", propertyTypes[0].Alias); // just to be sure
                Assert.IsNull(propertyTypes[0].PropertyGroupId);
                Assert.IsTrue(propertyTypes.Skip(1).All((x => x.PropertyGroupId.Value == groupId)));
            }

        }

        [Test]
        public void Can_Perform_Update_On_ContentTypeRepository()
        {
            // Arrange
            var provider = TestObjects.GetScopeProvider(LoggerFactory);
            using (var scope = provider.CreateScope())
            {
                var repository = CreateRepository((IScopeAccessor) provider);
                // Act
                var contentType = repository.Get(NodeDto.NodeIdSeed + 1);

                contentType.Thumbnail = "Doc2.png";
                contentType.PropertyGroups["Content"].PropertyTypes.Add(new PropertyType(ShortStringHelper, "test", ValueStorageType.Ntext, "subtitle")
                {
                    Name = "Subtitle",
                    Description = "Optional Subtitle",
                    Mandatory = false,
                    SortOrder = 1,
                    DataTypeId = -88
                });
                repository.Save(contentType);


                var dirty = contentType.IsDirty();

                // Assert
                Assert.That(contentType.HasIdentity, Is.True);
                Assert.That(dirty, Is.False);
                Assert.That(contentType.Thumbnail, Is.EqualTo("Doc2.png"));
                Assert.That(contentType.PropertyTypes.Any(x => x.Alias == "subtitle"), Is.True);
            }


        }

        // this is for tests only because it makes no sense at all to have such a
        // mapping defined, we only need it for the weird tests that use it
        private DocumentTypeSave MapToContentTypeSave(DocumentTypeDisplay display)
        {
            return new DocumentTypeSave
            {
                // EntityBasic
                Name = display.Name,
                Icon = display.Icon,
                Trashed = display.Trashed,
                Key = display.Key,
                ParentId = display.ParentId,
                //Alias = display.Alias,
                Path = display.Path,
                //AdditionalData = display.AdditionalData,

                // ContentTypeBasic
                Alias = display.Alias,
                UpdateDate = display.UpdateDate,
                CreateDate = display.CreateDate,
                Description = display.Description,
                Thumbnail = display.Thumbnail,

                // ContentTypeSave
                CompositeContentTypes = display.CompositeContentTypes,
                IsContainer = display.IsContainer,
                AllowAsRoot = display.AllowAsRoot,
                AllowedTemplates = display.AllowedTemplates.Select(x => x.Alias),
                AllowedContentTypes = display.AllowedContentTypes,
                DefaultTemplate = display.DefaultTemplate == null ? null : display.DefaultTemplate.Alias,
                Groups = display.Groups.Select(x => new PropertyGroupBasic<PropertyTypeBasic>
                {
                    Inherited = x.Inherited,
                    Id = x.Id,
                    Properties = x.Properties,
                    SortOrder = x.SortOrder,
                    Name = x.Name
                }).ToArray()
            };
        }

        [Test]
        public void Can_Perform_Update_On_ContentTypeRepository_After_Model_Mapping()
        {
            // Arrange
            var provider = TestObjects.GetScopeProvider(LoggerFactory);
            using (var scope = provider.CreateScope())
            {
                var repository = CreateRepository((IScopeAccessor) provider);
                // Act
                var contentType = repository.Get(NodeDto.NodeIdSeed + 1);

                // there is NO mapping from display to contentType, but only from save
                // to contentType, so if we want to test, let's to it properly!
                var display = Mapper.Map<DocumentTypeDisplay>(contentType);
                var save = MapToContentTypeSave(display);

                // modify...
                save.Thumbnail = "Doc2.png";
                var contentGroup = save.Groups.Single(x => x.Name == "Content");
                contentGroup.Properties = contentGroup.Properties.Concat(new[]
                {
                    new PropertyTypeBasic
                    {
                        Alias = "subtitle",
                        Label = "Subtitle",
                        Description = "Optional Subtitle",
                        Validation = new PropertyTypeValidation
                        {
                            Mandatory = false,
                            Pattern = ""
                        },
                        SortOrder = 1,
                        DataTypeId = -88
                    }
                });

                var mapped = Mapper.Map(save, contentType);

                // just making sure
                Assert.AreEqual(mapped.Thumbnail, "Doc2.png");
                Assert.IsTrue(mapped.PropertyTypes.Any(x => x.Alias == "subtitle"));

                repository.Save(mapped);


                var dirty = mapped.IsDirty();

                //re-get
                contentType = repository.Get(NodeDto.NodeIdSeed + 1);

                // Assert
                Assert.That(contentType.HasIdentity, Is.True);
                Assert.That(dirty, Is.False);
                Assert.That(contentType.Thumbnail, Is.EqualTo("Doc2.png"));
                Assert.That(contentType.PropertyTypes.Any(x => x.Alias == "subtitle"), Is.True);
                foreach (var propertyType in contentType.PropertyTypes)
                {
                    Assert.IsTrue(propertyType.HasIdentity);
                    Assert.Greater(propertyType.Id, 0);
                }
            }
        }

        [Test]
        public void Can_Perform_Delete_On_ContentTypeRepository()
        {
            // Arrange
            var provider = TestObjects.GetScopeProvider(LoggerFactory);
            using (var scope = provider.CreateScope())
            {
                var repository = CreateRepository((IScopeAccessor) provider);
                // Act
                var contentType = MockedContentTypes.CreateSimpleContentType();
                repository.Save(contentType);


                var contentType2 = repository.Get(contentType.Id);
                repository.Delete(contentType2);


                var exists = repository.Exists(contentType.Id);

                // Assert
                Assert.That(exists, Is.False);
            }
        }

        [Test]
        public void Can_Perform_Delete_With_Heirarchy_On_ContentTypeRepository()
        {
            // Arrange
            var provider = TestObjects.GetScopeProvider(LoggerFactory);
            using (var scope = provider.CreateScope())
            {
                var repository = CreateRepository((IScopeAccessor) provider);
                var ctMain = MockedContentTypes.CreateSimpleContentType();
                var ctChild1 = MockedContentTypes.CreateSimpleContentType("child1", "Child 1", ctMain, true);
                var ctChild2 = MockedContentTypes.CreateSimpleContentType("child2", "Child 2", ctChild1, true);

                repository.Save(ctMain);
                repository.Save(ctChild1);
                repository.Save(ctChild2);


                // Act

                var resolvedParent = repository.Get(ctMain.Id);
                repository.Delete(resolvedParent);


                // Assert
                Assert.That(repository.Exists(ctMain.Id), Is.False);
                Assert.That(repository.Exists(ctChild1.Id), Is.False);
                Assert.That(repository.Exists(ctChild2.Id), Is.False);
            }
        }

        [Test]
        public void Can_Perform_Query_On_ContentTypeRepository_Sort_By_Name()
        {
            IContentType contentType;

            // Arrange
            var provider = TestObjects.GetScopeProvider(LoggerFactory);
            using (var scope = provider.CreateScope())
            {
                var repository = CreateRepository((IScopeAccessor) provider);
                contentType = repository.Get(NodeDto.NodeIdSeed + 1);
                var child1 = MockedContentTypes.CreateSimpleContentType("abc", "abc", contentType, randomizeAliases: true);
                repository.Save(child1);
                var child3 = MockedContentTypes.CreateSimpleContentType("zyx", "zyx", contentType, randomizeAliases: true);
                repository.Save(child3);
                var child2 = MockedContentTypes.CreateSimpleContentType("a123", "a123", contentType, randomizeAliases: true);
                repository.Save(child2);

                scope.Complete();
            }

            using (var scope = provider.CreateScope())
            {
                var repository = CreateRepository((IScopeAccessor)provider);

                // Act
                var contentTypes = repository.Get(scope.SqlContext.Query<IContentType>().Where(x => x.ParentId == contentType.Id));

                // Assert
                Assert.That(contentTypes.Count(), Is.EqualTo(3));
                Assert.AreEqual("a123", contentTypes.ElementAt(0).Name);
                Assert.AreEqual("abc", contentTypes.ElementAt(1).Name);
                Assert.AreEqual("zyx", contentTypes.ElementAt(2).Name);
            }

        }

        [Test]
        public void Can_Perform_Get_On_ContentTypeRepository()
        {
            // Arrange
            var provider = TestObjects.GetScopeProvider(LoggerFactory);
            using (var scope = provider.CreateScope())
            {
                var repository = CreateRepository((IScopeAccessor) provider);

                // Act
                var contentType = repository.Get(NodeDto.NodeIdSeed + 1);

                // Assert
                Assert.That(contentType, Is.Not.Null);
                Assert.That(contentType.Id, Is.EqualTo(NodeDto.NodeIdSeed + 1));
            }
        }

        [Test]
        public void Can_Perform_Get_By_Guid_On_ContentTypeRepository()
        {
            // Arrange
            var provider = TestObjects.GetScopeProvider(LoggerFactory);
            using (var scope = provider.CreateScope())
            {
                var repository = CreateRepository((IScopeAccessor) provider);
                var contentType = repository.Get(NodeDto.NodeIdSeed + 1);
                var childContentType = MockedContentTypes.CreateSimpleContentType("blah", "Blah", contentType, randomizeAliases:true);
                repository.Save(childContentType);


                // Act
                var result = repository.Get(childContentType.Key);

                // Assert
                Assert.That(result, Is.Not.Null);
                Assert.That(result.Id, Is.EqualTo(childContentType.Id));
            }
        }

        [Test]
        public void Can_Perform_Get_By_Missing_Guid_On_ContentTypeRepository()
        {
            // Arrange
            var provider = TestObjects.GetScopeProvider(LoggerFactory);
            using (var scope = provider.CreateScope())
            {
                var repository = CreateRepository((IScopeAccessor) provider);
                // Act
                var result = repository.Get(Guid.NewGuid());

                // Assert
                Assert.That(result, Is.Null);
            }
        }

        [Test]
        public void Can_Perform_GetAll_On_ContentTypeRepository()
        {
            // Arrange
            var provider = TestObjects.GetScopeProvider(LoggerFactory);
            using (var scope = provider.CreateScope())
            {
                var repository = CreateRepository((IScopeAccessor) provider);

                // Act
                var contentTypes = repository.GetMany();
                int count =
                    scope.Database.ExecuteScalar<int>(
                        "SELECT COUNT(*) FROM umbracoNode WHERE nodeObjectType = @NodeObjectType",
                        new {NodeObjectType = Constants.ObjectTypes.DocumentType});

                // Assert
                Assert.That(contentTypes.Any(), Is.True);
                Assert.That(contentTypes.Count(), Is.EqualTo(count));
            }
        }

        [Test]
        public void Can_Perform_GetAll_By_Guid_On_ContentTypeRepository()
        {
            // Arrange
            var provider = TestObjects.GetScopeProvider(LoggerFactory);
            using (var scope = provider.CreateScope())
            {
                var repository = CreateRepository((IScopeAccessor) provider);
                var allGuidIds = repository.GetMany().Select(x => x.Key).ToArray();

                // Act
                var contentTypes = ((IReadRepository<Guid, IContentType>)repository).GetMany(allGuidIds);
                int count =
                    scope.Database.ExecuteScalar<int>(
                        "SELECT COUNT(*) FROM umbracoNode WHERE nodeObjectType = @NodeObjectType",
                        new { NodeObjectType = Constants.ObjectTypes.DocumentType });

                // Assert
                Assert.That(contentTypes.Any(), Is.True);
                Assert.That(contentTypes.Count(), Is.EqualTo(count));
            }
        }

        [Test]
        public void Can_Perform_Exists_On_ContentTypeRepository()
        {
            // Arrange
            var provider = TestObjects.GetScopeProvider(LoggerFactory);
            using (var scope = provider.CreateScope())
            {
                var repository = CreateRepository((IScopeAccessor) provider);

                // Act
                var exists = repository.Exists(NodeDto.NodeIdSeed);

                // Assert
                Assert.That(exists, Is.True);
            }
        }

        [Test]
        public void Can_Update_ContentType_With_PropertyType_Removed()
        {
            // Arrange
            var provider = TestObjects.GetScopeProvider(LoggerFactory);
            using (var scope = provider.CreateScope())
            {
                var repository = CreateRepository((IScopeAccessor) provider);
                var contentType = repository.Get(NodeDto.NodeIdSeed + 1);

                // Act
                contentType.PropertyGroups["Meta"].PropertyTypes.Remove("description");
                repository.Save(contentType);


                var result = repository.Get(NodeDto.NodeIdSeed + 1);

                // Assert
                Assert.That(result.PropertyTypes.Any(x => x.Alias == "description"), Is.False);
                Assert.That(contentType.PropertyGroups.Count, Is.EqualTo(result.PropertyGroups.Count));
                Assert.That(contentType.PropertyTypes.Count(), Is.EqualTo(result.PropertyTypes.Count()));
            }
        }

        [Test]
        public void Can_Verify_PropertyTypes_On_SimpleTextpage()
        {
            // Arrange
            var provider = TestObjects.GetScopeProvider(LoggerFactory);
            using (var scope = provider.CreateScope())
            {
                var repository = CreateRepository((IScopeAccessor) provider);

                // Act
                var contentType = repository.Get(NodeDto.NodeIdSeed);

                // Assert
                Assert.That(contentType.PropertyTypes.Count(), Is.EqualTo(3));
                Assert.That(contentType.PropertyGroups.Count(), Is.EqualTo(1));
            }
        }

        [Test]
        public void Can_Verify_PropertyTypes_On_Textpage()
        {
            // Arrange
            var provider = TestObjects.GetScopeProvider(LoggerFactory);
            using (var scope = provider.CreateScope())
            {
                var repository = CreateRepository((IScopeAccessor) provider);

                // Act
                var contentType = repository.Get(NodeDto.NodeIdSeed + 1);

                // Assert
                Assert.That(contentType.PropertyTypes.Count(), Is.EqualTo(4));
                Assert.That(contentType.PropertyGroups.Count(), Is.EqualTo(2));
            }
        }

        [Test]
        public void Can_Verify_PropertyType_With_No_Group()
        {
            // Arrange
            var provider = TestObjects.GetScopeProvider(LoggerFactory);
            using (var scope = provider.CreateScope())
            {
                var repository = CreateRepository((IScopeAccessor) provider);
                var contentType = repository.Get(NodeDto.NodeIdSeed + 1);

                Assert.That(contentType.PropertyGroups.Count, Is.EqualTo(2));
                Assert.That(contentType.PropertyTypes.Count(), Is.EqualTo(4));

                // Act
                var urlAlias = new PropertyType(ShortStringHelper, "test", ValueStorageType.Nvarchar, "urlAlias")
                    {
                        Name = "Url Alias",
                        Description = "",
                        Mandatory = false,
                        SortOrder = 1,
                        DataTypeId = -88
                    };

                var addedPropertyType = contentType.AddPropertyType(urlAlias);

                Assert.That(contentType.PropertyGroups.Count, Is.EqualTo(2));
                Assert.That(contentType.PropertyTypes.Count(), Is.EqualTo(5));

                repository.Save(contentType);


                // Assert
                var updated = repository.Get(NodeDto.NodeIdSeed + 1);
                Assert.That(addedPropertyType, Is.True);
                Assert.That(updated.PropertyGroups.Count, Is.EqualTo(2));
                Assert.That(updated.PropertyTypes.Count(), Is.EqualTo(5));
                Assert.That(updated.PropertyTypes.Any(x => x.Alias == "urlAlias"), Is.True);
                Assert.That(updated.PropertyTypes.First(x => x.Alias == "urlAlias").PropertyGroupId, Is.Null);
            }
        }

        [Test]
        public void Can_Verify_AllowedChildContentTypes_On_ContentType()
        {
            // Arrange
            var provider = TestObjects.GetScopeProvider(LoggerFactory);
            using (var scope = provider.CreateScope())
            {
                var repository = CreateRepository((IScopeAccessor) provider);

                var subpageContentType = MockedContentTypes.CreateSimpleContentType("umbSubpage", "Subpage");
                var simpleSubpageContentType = MockedContentTypes.CreateSimpleContentType("umbSimpleSubpage", "Simple Subpage");
                repository.Save(subpageContentType);
                repository.Save(simpleSubpageContentType);


                // Act
                var contentType = repository.Get(NodeDto.NodeIdSeed);
                contentType.AllowedContentTypes = new List<ContentTypeSort>
                    {
                        new ContentTypeSort(new Lazy<int>(() => subpageContentType.Id), 0, subpageContentType.Alias),
                        new ContentTypeSort(new Lazy<int>(() => simpleSubpageContentType.Id), 1, simpleSubpageContentType.Alias)
                    };
                repository.Save(contentType);


                //Assert
                var updated = repository.Get(NodeDto.NodeIdSeed);

                Assert.That(updated.AllowedContentTypes.Any(), Is.True);
                Assert.That(updated.AllowedContentTypes.Any(x => x.Alias == subpageContentType.Alias), Is.True);
                Assert.That(updated.AllowedContentTypes.Any(x => x.Alias == simpleSubpageContentType.Alias), Is.True);
            }
        }

        [Test]
        public void Can_Verify_Removal_Of_Used_PropertyType_From_ContentType()
        {
            // Arrange
            var provider = TestObjects.GetScopeProvider(LoggerFactory);
            using (var scope = provider.CreateScope())
            {
                ContentTypeRepository repository;
                var contentRepository = CreateRepository((IScopeAccessor) provider, out repository);
                var contentType = repository.Get(NodeDto.NodeIdSeed + 1);

                var subpage = MockedContent.CreateTextpageContent(contentType, "Text Page 1", contentType.Id);
                contentRepository.Save(subpage);


                // Act
                contentType.RemovePropertyType("keywords");
                repository.Save(contentType);


                // Assert
                Assert.That(contentType.PropertyTypes.Count(), Is.EqualTo(3));
                Assert.That(contentType.PropertyTypes.Any(x => x.Alias == "keywords"), Is.False);
                Assert.That(subpage.Properties.First(x => x.Alias == "description").GetValue(), Is.EqualTo("This is the meta description for a textpage"));
            }
        }

        [Test]
        public void Can_Verify_Addition_Of_PropertyType_After_ContentType_Is_Used()
        {
            // Arrange
            var provider = TestObjects.GetScopeProvider(LoggerFactory);
            using (var scope = provider.CreateScope())
            {
                ContentTypeRepository repository;
                var contentRepository = CreateRepository((IScopeAccessor) provider, out repository);
                var contentType = repository.Get(NodeDto.NodeIdSeed + 1);

                var subpage = MockedContent.CreateTextpageContent(contentType, "Text Page 1", contentType.Id);
                contentRepository.Save(subpage);


                // Act
                var propertyGroup = contentType.PropertyGroups.First(x => x.Name == "Meta");
                propertyGroup.PropertyTypes.Add(new PropertyType(ShortStringHelper, "test", ValueStorageType.Ntext, "metaAuthor") { Name = "Meta Author", Description = "", Mandatory = false, SortOrder = 1, DataTypeId = -88 });
                repository.Save(contentType);


                // Assert
                Assert.That(contentType.PropertyTypes.Count(), Is.EqualTo(5));
                Assert.That(contentType.PropertyTypes.Any(x => x.Alias == "metaAuthor"), Is.True);
            }

        }

        [Test]
        public void Can_Verify_Usage_Of_New_PropertyType_On_Content()
        {
            // Arrange
            var provider = TestObjects.GetScopeProvider(LoggerFactory);
            using (var scope = provider.CreateScope())
            {
                ContentTypeRepository repository;
                var contentRepository = CreateRepository((IScopeAccessor) provider, out repository);
                var contentType = repository.Get(NodeDto.NodeIdSeed + 1);

                var subpage = MockedContent.CreateTextpageContent(contentType, "Text Page 1", contentType.Id);
                contentRepository.Save(subpage);


                var propertyGroup = contentType.PropertyGroups.First(x => x.Name == "Meta");
                propertyGroup.PropertyTypes.Add(new PropertyType(ShortStringHelper, "test", ValueStorageType.Ntext, "metaAuthor") { Name = "Meta Author", Description = "", Mandatory = false, SortOrder = 1, DataTypeId = -88 });
                repository.Save(contentType);


                // Act
                var content = contentRepository.Get(subpage.Id);
                content.SetValue("metaAuthor", "John Doe");
                contentRepository.Save(content);


                //Assert
                var updated = contentRepository.Get(subpage.Id);
                Assert.That(updated.GetValue("metaAuthor").ToString(), Is.EqualTo("John Doe"));
                Assert.That(contentType.PropertyTypes.Count(), Is.EqualTo(5));
                Assert.That(contentType.PropertyTypes.Any(x => x.Alias == "metaAuthor"), Is.True);
            }
        }

        [Test]
        public void Can_Verify_That_A_Combination_Of_Adding_And_Deleting_PropertyTypes_Doesnt_Cause_Issues_For_Content_And_ContentType()
        {
            // Arrange
            var provider = TestObjects.GetScopeProvider(LoggerFactory);
            using (var scope = provider.CreateScope())
            {
                ContentTypeRepository repository;
                var contentRepository = CreateRepository((IScopeAccessor) provider, out repository);
                var contentType = repository.Get(NodeDto.NodeIdSeed + 1);

                var subpage = MockedContent.CreateTextpageContent(contentType, "Text Page 1", contentType.Id);
                contentRepository.Save(subpage);


                //Remove PropertyType
                contentType.RemovePropertyType("keywords");
                //Add PropertyType
                var propertyGroup = contentType.PropertyGroups.First(x => x.Name == "Meta");
                propertyGroup.PropertyTypes.Add(new PropertyType(ShortStringHelper, "test", ValueStorageType.Ntext, "metaAuthor") { Name = "Meta Author", Description = "",  Mandatory = false, SortOrder = 1, DataTypeId = -88 });
                repository.Save(contentType);


                // Act
                var content = contentRepository.Get(subpage.Id);
                content.SetValue("metaAuthor", "John Doe");
                contentRepository.Save(content);


                //Assert
                var updated = contentRepository.Get(subpage.Id);
                Assert.That(updated.GetValue("metaAuthor").ToString(), Is.EqualTo("John Doe"));
                Assert.That(updated.Properties.First(x => x.Alias == "description").GetValue(), Is.EqualTo("This is the meta description for a textpage"));

                Assert.That(contentType.PropertyTypes.Count(), Is.EqualTo(4));
                Assert.That(contentType.PropertyTypes.Any(x => x.Alias == "metaAuthor"), Is.True);
                Assert.That(contentType.PropertyTypes.Any(x => x.Alias == "keywords"), Is.False);
            }
        }

        [Test]
        public void Can_Verify_Content_Type_Has_Content_Nodes()
        {
            // Arrange
            var provider = TestObjects.GetScopeProvider(LoggerFactory);
            using (var scope = provider.CreateScope())
            {
                ContentTypeRepository repository;
                var contentRepository = CreateRepository((IScopeAccessor)provider, out repository);
                var contentTypeId = NodeDto.NodeIdSeed + 1;
                var contentType = repository.Get(contentTypeId);

                // Act
                var result = repository.HasContentNodes(contentTypeId);

                var subpage = MockedContent.CreateTextpageContent(contentType, "Test Page 1", contentType.Id);
                contentRepository.Save(subpage);

                var result2 = repository.HasContentNodes(contentTypeId);

                // Assert
                Assert.That(result, Is.False);
                Assert.That(result2, Is.True);
            }
        }

        public void CreateTestData()
        {
            //Create and Save ContentType "umbTextpage" -> (NodeDto.NodeIdSeed)
            ContentType simpleContentType = MockedContentTypes.CreateSimpleContentType("umbTextpage", "Textpage");
            ServiceContext.ContentTypeService.Save(simpleContentType);

            //Create and Save ContentType "textPage" -> (NodeDto.NodeIdSeed + 1)
            ContentType textpageContentType = MockedContentTypes.CreateTextPageContentType();
            ServiceContext.ContentTypeService.Save(textpageContentType);
        }
    }
}
