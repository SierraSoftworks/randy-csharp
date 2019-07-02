using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Rex.Models;

namespace Rex.Controllers
{
    public abstract class CollectionController<T> : ControllerBase
        where T : IView<Collection>
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
            await GetUserCollectionOrCreateAsync();

            return (await Store.GetCollectionsAsync(User.GetOid()).ToEnumerable()).Select(Representer.ToView);
        }

        [HttpGet]
        [Route("api/[area]/collection", Name = "GetCollectionForUser.[area]")]
        [Route("api/[area]/collection/{id:Guid}", Name = "GetCollection.[area]")]
        [Authorize(Scopes.CollectionsRead, Roles = "Administrator,User")]
        public virtual async Task<T> Get(Guid? id)
        {
            if ((id ?? User.GetOid()) == User.GetOid())
                return await GetUserCollectionOrCreateAsync();
            return Representer.ToViewSafe(await this.Store.GetCollectionAsync(User.GetOid(), id ?? User.GetOid()));
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

            var added = await Store.StoreCollectionAsync(model);

            await RoleStore.StoreRoleAssignmentAsync(new RoleAssignment
            {
                CollectionId = added.CollectionId,
                PrincipalId = added.PrincipalId,
                Role = RoleAssignment.Owner
            });

            var area = this.RouteData.Values["area"];
            return this.CreatedAtRoute($"GetCollection.{area}", new { id = added.CollectionId.ToString("N") }, Representer.ToView(added));
        }

        [HttpDelete]
        [Route("api/[area]/collection/{id:Guid}", Name = "RemoveCollection.[area]")]
        [Authorize(Scopes.CollectionsWrite, Roles = "Administrator,User")]
        public virtual async Task<ActionResult> Delete(Guid id)
        {
            var userOid = this.User.GetOid();

            var userRole = await this.RoleStore.GetRoleAssignment(id, userOid);
            if (userRole?.Role == RoleAssignment.Owner)
            {
                var roleAssignments = await RoleStore.GetRoleAssignments(id).ToEnumerable();
                if (roleAssignments.Count(c => c.PrincipalId != userOid && c.Role == RoleAssignment.Owner) == 0)
                {
                    return this.BadRequest();
                }
            }

            if (!await this.Store.RemoveCollectionAsync(id, this.User.GetOid()))
            {
                return this.NotFound();
            }

            await this.RoleStore.RemoveRoleAssignmentAsync(this.User.GetOid(), id);

            return this.NoContent();
        }

        private async Task<T> GetUserCollectionOrCreateAsync()
        {
            var userOid = User.GetOid();

            var collection = await Store.GetCollectionAsync(userOid, userOid);
            if (collection == null)
            {
                collection = await Store.StoreCollectionAsync(new Collection
                {
                    CollectionId = userOid,
                    PrincipalId = userOid,
                    Name = "Your Ideas",
                });

                await RoleStore.StoreRoleAssignmentAsync(new RoleAssignment
                {
                    CollectionId = userOid,
                    PrincipalId = userOid,
                    Role = RoleAssignment.Owner,
                });
            }

            return Representer.ToView(collection);
        }
    }
}
