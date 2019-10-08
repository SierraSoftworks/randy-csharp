using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Rex.Models;
using SierraLib.API.Views;

namespace Rex.Controllers
{
    public abstract class CollectionController<T> : ControllerBase
        where T : class, IView<Collection>
    {
        public CollectionController(Stores.ICollectionStore store, Stores.IRoleAssignmentStore roleStore, IRepresenter<Collection, T> representer)
        {
            Store = store;
            RoleStore = roleStore;
            Representer = representer;
        }

        protected IRepresenter<Collection, T> Representer { get; }

        protected Stores.ICollectionStore Store { get; }

        protected Stores.IRoleAssignmentStore RoleStore { get; }

        [HttpGet]
        [Route("api/[area]/collections", Name = "GetCollections.[area]")]
        [Authorize(Scopes.CollectionsRead, Roles = "Administrator,User")]
        public virtual async Task<IEnumerable<T>> List()
        {
            await GetUserCollectionOrCreateAsync().ConfigureAwait(false);

            return (await Store.GetCollectionsAsync(User.GetOid()).ToEnumerable().ConfigureAwait(false)).Select(Representer.ToView);
        }

        [HttpGet]
        [Route("api/[area]/collection", Name = "GetCollectionForUser.[area]")]
        [Route("api/[area]/collection/{id:Guid}", Name = "GetCollection.[area]")]
        [Authorize(Scopes.CollectionsRead, Roles = "Administrator,User")]
        public virtual async Task<ActionResult<T>> GetCollection(Guid? id)
        {
            if ((id ?? User.GetOid()) == User.GetOid())
                return await GetUserCollectionOrCreateAsync().ConfigureAwait(false);
            return Representer.ToViewOrDefault(await Store.GetCollectionAsync(User.GetOid(), id ?? User.GetOid()).ConfigureAwait(false)).ToActionResult() ?? this.NotFound();
        }

        [HttpPost]
        [Route("api/[area]/collections", Name = "CreateCollection.[area]")]
        [Authorize(Scopes.CollectionsWrite, Roles = "Administrator,User")]
        public virtual async Task<ActionResult<T>> Add(T collection)
        {
            var model = Representer.ToModel(collection);

            if (model.CollectionId == Guid.Empty)
            {
                model.CollectionId = Guid.NewGuid();
            }

            model.PrincipalId = this.User.GetOid();

            if (string.IsNullOrEmpty(model.Name))
            {
                return this.BadRequest();
            }

            var added = await Store.StoreCollectionAsync(model).ConfigureAwait(false);

            await RoleStore.StoreRoleAssignmentAsync(new RoleAssignment
            {
                CollectionId = added.CollectionId,
                PrincipalId = added.PrincipalId,
                Role = RoleAssignment.Owner
            }).ConfigureAwait(false);

            var area = this.RouteData.Values["area"];
            return this.CreatedAtRoute($"GetCollection.{area}", new { id = added.CollectionId.ToString("N", CultureInfo.InvariantCulture) }, Representer.ToView(added));
        }

        [HttpDelete]
        [Route("api/[area]/collection/{id:Guid}", Name = "RemoveCollection.[area]")]
        [Authorize(Scopes.CollectionsWrite, Roles = "Administrator,User")]
        public virtual async Task<ActionResult> Delete(Guid id)
        {
            var userOid = this.User.GetOid();

            var userRole = await RoleStore.GetRoleAssignment(id, userOid).ConfigureAwait(false);
            if (userRole?.Role == RoleAssignment.Owner)
            {
                var roleAssignments = await RoleStore.GetRoleAssignments(id).ToEnumerable().ConfigureAwait(false);
                if (!roleAssignments.Any(c => c.PrincipalId != userOid && c.Role == RoleAssignment.Owner))
                {
                    return this.BadRequest();
                }
            }

            if (!await Store.RemoveCollectionAsync(id, User.GetOid()).ConfigureAwait(false))
            {
                return this.NotFound();
            }

            await RoleStore.RemoveRoleAssignmentAsync(User.GetOid(), id).ConfigureAwait(false);

            return this.NoContent();
        }

        private async Task<T> GetUserCollectionOrCreateAsync()
        {
            var userOid = User.GetOid();

            var collection = await Store.GetCollectionAsync(userOid, userOid).ConfigureAwait(false);
            if (collection == null)
            {
                collection = await Store.StoreCollectionAsync(new Collection
                {
                    CollectionId = userOid,
                    PrincipalId = userOid,
                    Name = "Your Ideas",
                }).ConfigureAwait(false);

                await RoleStore.StoreRoleAssignmentAsync(new RoleAssignment
                {
                    CollectionId = userOid,
                    PrincipalId = userOid,
                    Role = RoleAssignment.Owner,
                }).ConfigureAwait(false);
            }

            return Representer.ToView(collection);
        }
    }
}
