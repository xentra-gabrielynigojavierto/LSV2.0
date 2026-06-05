pipeline {
    agent {
        kubernetes {
            yaml '''
apiVersion: v1
kind: Pod
metadata:
  namespace: tools
spec:
  containers:

  # =====================================================
  # ✅ DOCKER-IN-DOCKER CONTAINER (BUILDS + PUSHES IMAGES)
  # =====================================================
  - name: docker
    image: docker:24.0.7-dind
    securityContext:
      privileged: true
    tty: true

  # =====================================================
  # ✅ AWS + KUBECTL TOOLS (DEPLOYMENT ONLY)
  # =====================================================
  - name: aws-k8s-tools
    image: amazon/aws-cli:2.15.15
    command: ["cat"]
    tty: true
'''
        }
    }

    # =====================================================
    # ✅ GLOBAL VARIABLES
    # =====================================================
    environment {
        AWS_REGION = 'us-east-1'
        AWS_ACCOUNT_ID = '637423518666'

        # ✅ ECR registry URL
        ECR_REGISTRY = "${AWS_ACCOUNT_ID}.dkr.ecr.${AWS_REGION}.amazonaws.com"

        # ✅ Versioned image tag
        IMAGE_TAG = "${BUILD_NUMBER}-${GIT_COMMIT.take(7)}"

        # ✅ EKS config
        EKS_CLUSTER_NAME = 'dev-eks-cluster'
        K8S_NAMESPACE = 'dev'
    }

    stages {

        # =====================================================
        # ✅ STAGE 1: DOCKER LOGIN (FIXED LOCATION)
        # =====================================================
        stage('Docker Login') {
            steps {
                container('docker') {

                    sh '''
                    # ✅ install aws CLI inside docker container
                    apk add --no-cache aws-cli

                    # ✅ login to ECR (credentials will persist in THIS container)
                    aws ecr get-login-password --region $AWS_REGION \
                    | docker login \
                        --username AWS \
                        --password-stdin $ECR_REGISTRY
                    '''
                }
            }
        }

        # =====================================================
        # ✅ STAGE 2: BUILD & PUSH (PARALLEL)
        # =====================================================
        stage('Parallel Build & Push') {

            parallel {

                # -----------------------------
                # ✅ TENANT APP
                # -----------------------------
                stage('Tenant App') {
                    steps {
                        container('docker') {
                            sh '''
                            docker build \
                              --build-arg APP_PORT=5000 \
                              -t $ECR_REGISTRY/tenant-app:$IMAGE_TAG \
                              ./tenant-app

                            docker push $ECR_REGISTRY/tenant-app:$IMAGE_TAG
                            '''
                        }
                    }
                }

                # -----------------------------
                # ✅ GATEWAY
                # -----------------------------
                stage('Gateway') {
                    steps {
                        container('docker') {
                            sh '''
                            docker build \
                              --build-arg SERVICE_PORT=5010 \
                              -t $ECR_REGISTRY/gateway-yarp:$IMAGE_TAG \
                              ./gateway

                            docker push $ECR_REGISTRY/gateway-yarp:$IMAGE_TAG
                            '''
                        }
                    }
                }

                # -----------------------------
                # ✅ IDENTITY SERVICE
                # -----------------------------
                stage('Identity Service') {
                    steps {
                        container('docker') {
                            sh '''
                            docker build \
                              --build-arg SERVICE_PORT=5001 \
                              -t $ECR_REGISTRY/identity-service:$IMAGE_TAG \
                              ./identity

                            docker push $ECR_REGISTRY/identity-service:$IMAGE_TAG
                            '''
                        }
                    }
                }

            }
        }

        # =====================================================
        # ✅ STAGE 3: DEPLOY TO EKS (FIXED)
        # =====================================================
        stage('Deploy to EKS') {
            steps {
                container('aws-k8s-tools') {

                    sh '''
                    # ✅ Configure kubeconfig for EKS
                    aws eks update-kubeconfig \
                      --region $AWS_REGION \
                      --name $EKS_CLUSTER_NAME

                    # ✅ Apply manifests
                    kubectl apply -f ./k8s/mesh-configuration/
                    '''
                }
            }
        }
    }
}