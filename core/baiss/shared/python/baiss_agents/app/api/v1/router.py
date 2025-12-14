from fastapi import APIRouter
from baiss_agents.app.api.v1.endpoints import baiss, files, models, chatv2

api_router = APIRouter()


# Include all endpoint routers with appropriate prefixes and tags
# api_router.include_router(
#     chat.router,
#     prefix="/chat",
#     tags=["AI Generation", "Tools"]
# )

api_router.include_router(
    baiss.router,
    prefix = "/baiss-app",
    tags   = ["Baiss Application"]
)

api_router.include_router(
    chatv2.router,
    prefix="/chatv2",
    tags=["AI Generation", "Tools"]
)

api_router.include_router(
    files.router,
    prefix = "/files",
    tags   = ["File Processing"]
)

api_router.include_router(
    models.router,
    prefix = "/models",
    tags   = ["models Processing"]
)

