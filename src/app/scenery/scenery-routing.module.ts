import { RouterModule, Routes } from '@angular/router';
import { SceneryPage } from './scenery.page';
import { NgModule } from '@angular/core';

const routes: Routes = [
    {
        path: 'tabs',
        component: SceneryPage,
        children: [
            {
                path: 'observe',
                children: [
                    {
                        path: '',
                        loadChildren: './observe/observe.module#ObservePageModule'
                    },
                    {
                        path: ':sceneId',
                        loadChildren: './observe/scene-detail/scene-detail.module#SceneDetailPageModule'
                    }
                ]
            },
            {
                path: 'my-scenes',
                children: [
                    {
                        path: '',
                        loadChildren: './my-scenes/my-scenes.module#MyScenesPageModule'
                    },
                     {
                        path: 'new',
                        loadChildren: './my-scenes/new-scene/new-scene.module#NewScenePageModule'
                    },
                    {
                        path: 'edit:/sceneId',
                        loadChildren: './my-scenes/edit-scene/edit-scene.module#EditScenePageModule'
                    },
                    {
                        path: ':sceneId',
                        loadChildren: './my-scenes/share-scenery/share-scenery.module#ShareSceneryPageModule'
                    }
                ]
            },
            {
                path: '',
                redirectTo: '/scenery/tabs/observe',
                pathMatch: 'full'
            }
        ]
    },
    {
        path: '',
        redirectTo: '/scenery/tabs/observe',
        pathMatch: 'full'
    }
];

@NgModule({
    imports: [RouterModule.forChild(routes)],
    exports: [RouterModule]
})

export class SceneryRoutingModule { }