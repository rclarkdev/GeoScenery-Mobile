export class User {
    constructor(
        public id: number,
        public displayName: string,
        public email: string | null,
        public profileImageUrl?: string,
        public latitude?: number,
        public longitude?: number,
        public birthDate?: string,
        public education?: string,
        public hobbies?: string,
        public employment?: string,
        public bio?: string,
        public followerCount = 0,
        public followingCount = 0,
        public isFollowedByCurrentUser = false,
        public createdAt?: string
    ) {

    }
}
