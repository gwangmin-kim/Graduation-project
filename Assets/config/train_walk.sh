DIR="/root/workspace/MLAgents"
NAME="WalkAgent"
EXE_NAME="Train_Walk.x86_64"

mlagents-learn "$DIR/config/Walk.yaml" \
    --torch-device=cuda \
    --run-id="PlayerAgent_Walk_$TAG" \
    --base-port=5004 \
    --env "$DIR/$NAME/$EXE_NAME" \
    --num-envs=128 \
    --no-graphics
